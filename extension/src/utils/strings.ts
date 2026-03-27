/**
 * Centralized UI strings for the DevOps Copilot extension.
 *
 * All user-facing text should be defined here to:
 * - Avoid hardcoding strings across components
 * - Enable future localization
 * - Allow pipeline variable replacement for environment-specific text
 *
 * Usage: import { STRINGS } from '../utils/strings';
 */

export const STRINGS = {
  // General
  appName: "DevOps Copilot",
  appDescription: "Your AI assistant for Azure DevOps",

  // Welcome screen
  welcome: {
    title: "Welcome to DevOps Copilot",
    subtitle: "Your AI assistant for Azure DevOps. What would you like to do?",
  },

  // Persona labels
  personas: {
    productOwner: "Product Owners",
    developer: "Developers",
    tester: "Testers",
    management: "Management",
  },

  // Quick action prompts (persona-based)
  quickActions: {
    productOwner: [
      {
        label: "Backlog health check",
        prompt:
          "Show backlog health — items missing acceptance criteria, unestimated, or without descriptions",
      },
      {
        label: "Sprint progress summary",
        prompt:
          "Summarize current sprint progress — completed, in progress, new, and total story points",
      },
      {
        label: "Stories missing AC",
        prompt:
          "Find user stories without acceptance criteria in the current sprint",
      },
    ],
    developer: [
      {
        label: "My active work items",
        prompt: "Show my active work items sorted by priority",
      },
      {
        label: "Blocked items",
        prompt: "What items are blocked or have impediments?",
      },
      {
        label: "My bugs needing attention",
        prompt: "Find bugs assigned to me that need attention",
      },
    ],
    tester: [
      {
        label: "Stories without test cases",
        prompt: "Find user stories in this sprint without test cases",
      },
      {
        label: "Bugs needing verification",
        prompt: "Show recently resolved bugs that need verification testing",
      },
      {
        label: "Generate sprint test cases",
        prompt:
          "Generate test cases for all active user stories in the current sprint",
      },
    ],
    management: [
      {
        label: "At-risk items",
        prompt: "Show items overdue or at risk in the current sprint",
      },
      {
        label: "Team workload overview",
        prompt:
          "Summarize team workload distribution — who has the most items and story points",
      },
      {
        label: "Sprint health check",
        prompt:
          "Sprint health check — scope changes, blockers, and completion forecast",
      },
    ],
  },

  // Work item type-specific suggestions
  workItemSuggestions: {
    story: [
      {
        label: "Rate INVEST criteria",
        prompt:
          "Rate this user story against INVEST criteria (Independent, Negotiable, Valuable, Estimable, Small, Testable) and score each",
      },
      {
        label: "Generate Given/When/Then AC",
        prompt:
          "Generate acceptance criteria for this user story in Given/When/Then format",
      },
      {
        label: "Generate test cases",
        prompt:
          "Generate comprehensive test cases for this user story covering happy path, edge cases, and error scenarios",
      },
      {
        label: "Suggest task breakdown",
        prompt: "Suggest a task breakdown for implementing this user story",
      },
      {
        label: "Create child tasks",
        prompt:
          "Create child tasks for this user story based on the implementation requirements. Read the story first, then create and auto-link each task.",
      },
      {
        label: "Estimate complexity",
        prompt:
          "Estimate the story points and complexity for this user story based on its scope",
      },
      {
        label: "What's missing?",
        prompt: "What's missing or could be improved in this user story?",
      },
    ],
    bug: [
      {
        label: "Analyze severity & impact",
        prompt: "Analyze the severity, impact, and affected areas of this bug",
      },
      {
        label: "Suggest repro steps",
        prompt: "Suggest improvements to the reproduction steps for this bug",
      },
      {
        label: "Find related/duplicate bugs",
        prompt: "Search for related or duplicate bugs similar to this one",
      },
      {
        label: "Generate regression tests",
        prompt: "Generate regression test cases to verify this bug fix",
      },
      {
        label: "Create test case for this bug",
        prompt:
          "Create a Test Case work item linked to this bug with regression test steps",
      },
      {
        label: "Root cause analysis",
        prompt: "Help analyze the potential root cause of this bug",
      },
      {
        label: "What's missing?",
        prompt: "What's missing or could be improved in this bug report?",
      },
    ],
    featureEpic: [
      {
        label: "Suggest story breakdown",
        prompt: "Suggest user stories to break down this feature/epic",
      },
      {
        label: "Create child stories",
        prompt:
          "Create child User Stories for this feature/epic. Read the context first, then create and auto-link each story.",
      },
      {
        label: "Estimate scope & effort",
        prompt:
          "Analyze the scope and estimate effort needed for this feature/epic",
      },
      {
        label: "Identify risks & dependencies",
        prompt:
          "Identify risks, dependencies, and potential blockers for implementing this",
      },
      {
        label: "Generate acceptance criteria",
        prompt: "Generate high-level acceptance criteria for this feature/epic",
      },
      {
        label: "What's missing?",
        prompt: "What's missing or could be improved in this feature/epic?",
      },
    ],
    task: [
      {
        label: "Clarify requirements",
        prompt:
          "Analyze this task and suggest any missing requirements or unclear areas",
      },
      {
        label: "Estimate remaining effort",
        prompt: "Help estimate the remaining effort for this task",
      },
      {
        label: "Find blocking dependencies",
        prompt:
          "Check for dependencies or blockers that could affect this task",
      },
    ],
    default: [
      {
        label: "Analyze quality",
        prompt: "Analyze the quality and completeness of this work item",
      },
      {
        label: "Suggest improvements",
        prompt:
          "What improvements or additions would you suggest for this work item?",
      },
    ],
  },

  // Chat messages
  chat: {
    sendPlaceholder: "Ask about your Azure DevOps project...",
    emptyState: "No messages yet. Start a conversation!",
    errorPrefix: "Sorry, I encountered an error:",
    fallbackResponse: "I wasn't able to process your request.",
    newChat: "New Chat",
    sessionHistory: "Session history",
    selectProject: "Select project",
    selectModel: "Select model",
    addWorkItemTag: "Add WI #",
  },

  // Action labels
  actions: {
    analyze: "Analyze",
    tests: "Tests",
    criteria: "Criteria",
    children: "Children",
    chat: "Chat",
    history: "History",
    openFullChat: "Open full chat",
    confirm: "Yes, go ahead",
    reject: "No, cancel that",
    retry: "Try rephrasing your request",
    checkConnection: "Check your connection settings",
  },

  // Session panel
  session: {
    title: "Chat History",
    searchPlaceholder: "Search sessions...",
    noSessions: "No chat sessions yet",
    today: "Today",
    yesterday: "Yesterday",
    thisWeek: "This Week",
    thisMonth: "This Month",
    older: "Older",
  },

  // Work item group
  workItemGroup: {
    resumePrompt: "You have a previous conversation about this work item.",
    resumeButton: "Resume",
    startNew: "Start new",
    noWorkItemId: "Save this work item first to use AI features.",
    emptyHint:
      "Ask me anything about this work item, or use the quick actions above.",
  },

  // Error messages
  errors: {
    invalidToken: "Invalid extension token.",
    missingAuth: "Missing Authorization bearer token.",
    invalidJson: "Invalid JSON request body.",
    messageRequired: "Message is required.",
    backendFailed: "Backend request failed",
    streamFailed: "Backend stream request failed",
    noResponseBody: "No response body",
    unknownError: "Unknown error occurred",
  },
} as const;

/**
 * Get work item type-specific suggestions based on the type string.
 */
export function getSuggestionsForType(
  workItemType: string,
): { label: string; prompt: string }[] {
  const type = workItemType.toLowerCase();

  if (/user story|story/.test(type))
    return [...STRINGS.workItemSuggestions.story];
  if (/bug|issue|defect/.test(type))
    return [...STRINGS.workItemSuggestions.bug];
  if (/feature|epic/.test(type))
    return [...STRINGS.workItemSuggestions.featureEpic];
  if (/task/.test(type)) return [...STRINGS.workItemSuggestions.task];

  return [...STRINGS.workItemSuggestions.default];
}
