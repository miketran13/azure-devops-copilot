# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in this project, please report it responsibly.

**Please do NOT open a public GitHub issue for security vulnerabilities.**

Instead, please send an email to the maintainers describing:

1. The type of vulnerability
2. Steps to reproduce
3. Potential impact
4. Suggested fix (if any)

We will acknowledge receipt within 48 hours and provide a detailed response within 7 days.

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| latest  | :white_check_mark: |

## Security Best Practices for Deployment

- **Never** commit `local.settings.json` or `.env` files
- Use **Azure Key Vault** for all secrets in production
- Use **Managed Identity** for Azure resource access
- Enable **HTTPS only** on Azure Functions
- Rotate API keys and PATs regularly
- Review Azure DevOps OAuth scopes — grant minimum required permissions
