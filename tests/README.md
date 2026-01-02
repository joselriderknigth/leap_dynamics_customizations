# Leap Dynamics Testing

This repository contains two main test projects that aim to run validation for the Leap dynamics system:

-   **e2e_api**: Contains tests for logic that can be validated purely through API calls. It heavily relies on the Dynamics API to perform CRUD operations.
-   **e2e_ui**: Contains tests for UI logic within the system, including client automations and form validations.

Refer to each project's documentation for setup and execution instructions.

## Prerequisites

1. .NET 9 SDK
2. Access to the Leap Dynamics environment

## VS Code

Since this is a monorepo, in order to get some of the features from VS Code you should open the project in a separate window from the /tests directory. This will enable easier development and more importantly will make sure the right context is loaded.

### Recommended Extensions

1. CSharpier
2. C# Dev Kit
3. C#
