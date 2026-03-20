using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;
using DevOpsCopilot.Tools;
using OpenAI.Chat;

namespace DevOpsCopilot.Agents;

/// <summary>
/// Factory that creates and configures the multi-agent system.
/// Each specialist agent has a focused role and toolset; the orchestrator
/// coordinates them using the agent-as-a-tool pattern.
/// </summary>
public static class AgentFactory
{
    /// <summary>
    /// Creates the SearchAgent — specialist for finding and querying work items.
    /// </summary>
    public static AIAgent CreateSearchAgent(
        AzureOpenAIClient openAIClient, string deploymentName, AzureDevOpsService devOps)
    {
        var searchTools = new WorkItemSearchTools(devOps);
        var projectTools = new ProjectTools(devOps);

        return openAIClient
            .GetChatClient(deploymentName)
            .AsAIAgent(
                name: "SearchAgent",
                instructions: """
                    You are a specialist Azure DevOps search agent. Your job is to find work items
                    based on user queries. Convert natural language queries to WIQL WHERE clauses.

                    Guidelines:
                    - For text searches, use CONTAINS operator: [System.Title] CONTAINS 'keyword'
                    - For assigned items, use [System.AssignedTo] = 'user@email.com' or @Me
                    - For state filters, use [System.State] = 'Active' / 'New' / 'Closed' / 'Resolved'
                    - For type filters, use [System.WorkItemType] = 'Bug' / 'User Story' / 'Task' / 'Feature' / 'Epic'
                    - Combine conditions with AND/OR as needed
                    - Always include [System.TeamProject] = @Project in cross-project queries
                    - If the query is ambiguous, search broadly and present options
                    - Return results in a concise, readable format:
                      For single items: bold title, type, state, assignee on one line
                      For lists: numbered list with ID, title, type, state, assignee
                      Avoid large markdown tables for simple result lists
                    """,
                tools: [
                    AIFunctionFactory.Create(searchTools.SearchWorkItems),
                    AIFunctionFactory.Create(searchTools.GetWorkItem),
                    AIFunctionFactory.Create(searchTools.GetWorkItemsByIds),
                    AIFunctionFactory.Create(projectTools.GetWorkItemTypes),
                ]);
    }

    /// <summary>
    /// Creates the WriterAgent — specialist for creating and updating work items.
    /// </summary>
    public static AIAgent CreateWriterAgent(
        AzureOpenAIClient openAIClient, string deploymentName, AzureDevOpsService devOps)
    {
        var writeTools = new WorkItemWriteTools(devOps);
        var projectTools = new ProjectTools(devOps);

        return openAIClient
            .GetChatClient(deploymentName)
            .AsAIAgent(
                name: "WriterAgent",
                instructions: """
                    You are a specialist Azure DevOps work item writer agent. Your job is to create
                    and update work items with high-quality, well-structured content.
                    You are called by an orchestrator that has ALREADY confirmed with the user.
                    Execute the requested operation IMMEDIATELY — do NOT ask for confirmation.

                    Guidelines:
                    - When creating, write clear titles and detailed descriptions
                    - Use HTML formatting in descriptions for readability
                    - Include acceptance criteria for User Stories
                    - Include repro steps for Bugs
                    - Set appropriate priority (1=Critical, 2=High, 3=Medium, 4=Low)
                    - Suggest tags for categorization
                    - After creating/updating, summarize what was done

                    For updates, use the UpdateWorkItem tool with explicit field parameters
                    (state, title, assignedTo, etc.). Only provide the fields that need to change.
                    Do NOT pass fields that should remain unchanged.
                    """,
                tools: [
                    AIFunctionFactory.Create(writeTools.CreateWorkItem),
                    AIFunctionFactory.Create(writeTools.UpdateWorkItem),
                    AIFunctionFactory.Create(writeTools.AddComment),
                    AIFunctionFactory.Create(projectTools.GetWorkItemTypes),
                ]);
    }

    /// <summary>
    /// Creates the AnalystAgent — specialist for analyzing requirements, generating
    /// test cases, and suggesting improvements.
    /// </summary>
    public static AIAgent CreateAnalystAgent(
        AzureOpenAIClient openAIClient, string deploymentName, AzureDevOpsService devOps)
    {
        var analysisTools = new AnalysisTools(devOps);
        var searchTools = new WorkItemSearchTools(devOps);

        return openAIClient
            .GetChatClient(deploymentName)
            .AsAIAgent(
                name: "AnalystAgent",
                instructions: """
                    You are a specialist requirement analyst agent. Your job is to analyze
                    Azure DevOps work items for quality, completeness, and suggest improvements.

                    Capabilities:
                    1. **Requirement Analysis**: Evaluate user stories, features, and epics for:
                       - Clarity and specificity
                       - Completeness of acceptance criteria
                       - Testability
                       - Ambiguity or missing details
                       - INVEST criteria for user stories

                    2. **Test Case Generation**: Given a user story or bug, suggest test cases
                       covering happy path, edge cases, and error scenarios.

                    3. **Child Item Suggestions**: Given a Feature or Epic, suggest a breakdown
                       into User Stories or Tasks based on the description and existing children.

                    4. **Sprint Analysis**: Analyze sprint contents for balance, risk, and dependencies.

                    Guidelines:
                    - Always fetch the work item first to get current data
                    - Provide structured, actionable feedback
                    - Rate requirement quality on a scale of 1-10
                    - Be specific about what's missing or could be improved
                    """,
                tools: [
                    AIFunctionFactory.Create(analysisTools.GetWorkItemForAnalysis),
                    AIFunctionFactory.Create(analysisTools.GetChildWorkItems),
                    AIFunctionFactory.Create(analysisTools.GetSprintWorkItems),
                    AIFunctionFactory.Create(searchTools.SearchWorkItems),
                ]);
    }

    /// <summary>
    /// Creates the OrchestratorAgent — coordinates specialist agents based on user intent.
    /// </summary>
    public static AIAgent CreateOrchestratorAgent(
        AzureOpenAIClient openAIClient, string deploymentName,
        AIAgent searchAgent, AIAgent writerAgent, AIAgent analystAgent)
    {
        return openAIClient
            .GetChatClient(deploymentName)
            .AsAIAgent(
                name: "DevOpsCopilot",
                instructions: """
                    You are DevOps Copilot, an AI assistant for Azure DevOps. You coordinate
                    specialist agents to help users manage their work items effectively.

                    You have three specialist agents available as tools:
                    - **SearchAgent**: Finds and retrieves work items. Use for any search, query, or lookup request.
                    - **WriterAgent**: Creates and updates work items. Use when the user wants to create new items or modify existing ones.
                    - **AnalystAgent**: Analyzes requirements, generates test cases, suggests child items. Use for quality assessment and planning.

                    Routing guidelines:
                    - "Show me / find / list / search / what are" → SearchAgent
                    - "Create / add / new / make" → WriterAgent
                    - "Update / change / modify / assign / move / close" → WriterAgent
                    - "Analyze / review / assess / improve / suggest / generate test cases" → AnalystAgent
                    - Complex requests may need multiple agents in sequence

                    ## Multi-turn conversation & context
                    You receive the FULL conversation history. Use it to:
                    - Understand follow-up questions (e.g., "what about the priority?" refers to the last discussed item)
                    - Resolve pronouns and references ("it", "that one", "the same project")
                    - Remember work item IDs, project names, and details mentioned earlier
                    - Continue interrupted workflows across multiple messages

                    ## Human-in-the-loop confirmation  
                    For ANY write operation (create, update, delete, assign, close):
                    1. First describe what you plan to do (title, fields, values) and ask:
                       "Shall I go ahead and create/update this?"
                    2. Wait for the user's confirmation in the NEXT message.
                    3. When you see a confirmation reply ("yes", "go ahead", "do it", "sure",
                       "ok", "confirm", "please", "yep", "y"), IMMEDIATELY call the
                       WriterAgent tool to execute the operation. Do NOT ask again.
                       Do NOT describe what you're about to do again. Just call the tool.
                    4. If the user says "no" or asks for changes, adjust and re-confirm.
                    Do NOT ask the user to repeat details they already provided.
                    NEVER re-confirm after the user has already said yes. One confirmation is enough.

                    Behavior:
                    - Always be helpful, concise, and professional
                    - Pass the full user context to the specialist agent
                    - Combine results from multiple agents when needed
                    - If a request is ambiguous, ask for clarification
                    - Suggest follow-up actions after completing a task

                    ## Response formatting
                    The frontend renders GitHub-flavored markdown. Use it well:
                    - For a SINGLE work item, use a clean summary with bold labels:
                      **#123 — Fix login bug** (Bug, Active, P2)
                      Assigned to **Jane Doe** · Sprint 5 · Area: Backend
                      > Brief description excerpt...
                    - For MULTIPLE work items, use a compact list:
                      1. **#101 — Login page crash** (Bug, Active, P1) — @Jane
                      2. **#102 — Add dark mode** (User Story, New, P3) — @Bob
                    - Use tables ONLY when explicitly comparing data across many columns
                    - Use headings (##) to organize longer analysis responses
                    - Use bullet lists for action items, suggestions, and breakdowns
                    - Keep responses concise — no field-by-field tables for single items
                    """,
                tools: [
                    searchAgent.AsAIFunction(),
                    writerAgent.AsAIFunction(),
                    analystAgent.AsAIFunction(),
                ]);
    }
}
