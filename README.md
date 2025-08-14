# ShieldChecker

<img src="LogoDesign/kit/Facebook-Kit.jpg" width="100%" />

ShieldChecker is a comprehensive community solution that allows testing established detections with Microsoft Defender XDR end-to-end. Unlike traditional approaches that simply replay logs, ShieldChecker actually executes tests and verifies that expected detections are triggered, providing real-world validation of your security controls.
More information can be found on the [Homepage](https://www.shieldchecker.ch).

## Overview

ShieldChecker is a comprehensive open-source security testing platform designed to validate Microsoft Defender XDR detections through real-world test execution. The platform combines:

- **End-to-End Security Testing** - Actually executes security tests rather than simply replaying logs
- **Microsoft Defender XDR Validation** - Verifies that expected detections are triggered in your environment
- **Azure-Native Architecture** - Built entirely on native Azure services and deployed in your own Azure tenant
- **Cost-Effective Operation** - Pay-as-you-go Azure pricing model with low monthly infrastructure costs (~$200 USD)
- **Multi-Platform Support** - Testing capabilities for both Windows and Linux environments
- **Domain Controller Testing** - Supports tests against domain controllers for comprehensive coverage
- **Atomic Red Team Integration** - Quick start with ability to import Atomic Red Team tests
- **Automated Scheduling** - Built-in scheduler for regular testing cycles without manual intervention
- **Simplified Review Process** - Streamlined error handling with dedicated RDP sessions for missed detections

### Key Benefits

- **Production Isolation** - Recommended deployment in dedicated test tenant to avoid interference with ML algorithms
- **Microsoft 365 E5 Ready** - One E5 subscription provides all necessary Defender XDR features
- **Full Automation** - Completely automated solution requiring minimal manual intervention
- **Open Source** - Available under GPL-3.0 license with community-driven development

## Architecture

The platform consists of several key components:

- **Function App** (`src/FunctionApp/`) - Azure Functions for serverless execution of security tests
- **Web Application** (`src/Webapp/`) - Frontend interface for managing and viewing security assessments
- **Executor** (`src/Executor/`) - Core execution engine for running security validations
- **Bicep Templates** (`src/Bicep/`) - Infrastructure as Code for Azure deployment
- **VM DSC** (`src/VmDsc/`) - PowerShell Desired State Configuration for virtual machine setup
- **Scheduler** (`Scheduler/`) - Task scheduling and orchestration components

## Getting Started

Check the [Deployment page](docs/Deployment.md) for detailed instructions regarding deployment.

## Deployment and Custom Build Options (For Code Contributors)

The project provides several deployment scripts:

| Script | Purpose |
|--------|---------|
| `Invoke-Build.ps1` | Build the solution locally |
| `Invoke-Deploy.ps1` | Deploy to Azure infrastructure |
| `Invoke-UpdateWebAppAndSql.ps1` | Update existing web app and database |

## Project Structure

```
src/
├── Bicep/          # Infrastructure as Code templates
├── Executor/       # Core execution engine
├── FunctionApp/    # Azure Functions
├── VmDsc/         # PowerShell DSC configurations
└── Webapp/        # Web application frontend

Deploy/
└── Latest/        # Latest deployment artifacts

Scheduler/         # Task scheduling components
├── ImportTests/   # Test import functionality

SupportiveContent/ # Additional resources and documentation
```

## Documentation

- [Homepage](https://www.shieldchecker.ch)
- [Deployment Guide](docs/Deployment.md) - Detailed deployment instructions
- [Documentation](docs/Documentation.md) - Comprehensive project documentation
- [Changelog](CHANGELOG.md) - Version history and updates

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests to ensure functionality by using Invoke-Build and followed by Invoke-Deploy.
5. Submit a pull request

Please see our [issue templates](.github/ISSUE_TEMPLATE/) for bug reports and feature requests.

## Support

For issues and support:
- Check existing [GitHub Issues](.github/ISSUE_TEMPLATE/)
- Review the [Documentation](/docs/Documentation.md)
- Consult the [Deployment Guide](/docs/Deployment.md)

There is no support or guaranteed answer. The project is a community project and maintained as a hobby.

## License

This project is licensed under the terms specified in the [LICENSE](LICENSE) file.
