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

- **Web Application** (`src/Webapp/`) - Frontend interface for managing and viewing security assessments
- **Backend API** (`src/Api/ShieldChecker.BackendApi/`) - Internal API consumed by the web application
- **HostService API** (`src/Api/ShieldChecker.HostServiceApi/`) - External API for host service communication
- **Import API** (`src/Api/ShieldChecker.ImportApi/`) - External API for importing tests from shared libraries
- **Data Access** (`src/Api/ShieldChecker.DataAccess/`) - Shared Entity Framework Core models and database context
- **Host Service** (`src/HostService/`) - Agent running on test machines to execute security tests
- **Bicep Templates** (`src/Bicep/`) - Infrastructure as Code for Azure deployment

## Getting Started

Check the [Deployment Guide](docs/Deployment.md) for detailed instructions regarding deployment.

## Deployment (For Code Contributors)

The project provides the following deployment script:

| Script | Purpose |
|--------|---------|
| `Invoke-Deploy.ps1` | All-in-one setup and update script covering app registrations, Bicep infrastructure, SQL setup, Docker build/push, and HostService packaging |

Individual phases can be skipped with `-Skip*` switches (e.g., `-SkipBicep`, `-SkipDockerBuild`) when only a subset of the stack needs to be refreshed.

To import Atomic Red Team tests into the shared library, use the import script:

| Script | Purpose |
|--------|---------|
| `src/Import-AtomicRedTeamToSharedLibrary.ps1` | Import Atomic Red Team tests via the Import API |

## Project Structure

```
src/
├── Api/
│   ├── ShieldChecker.BackendApi/       # Internal API (WebApp.Access)
│   ├── ShieldChecker.HostServiceApi/   # External API (HostService.Access)
│   ├── ShieldChecker.ImportApi/        # External API (Import.SharedLibrary)
│   └── ShieldChecker.DataAccess/       # Shared EF Core models
├── Bicep/                              # Infrastructure as Code templates
├── HostService/                        # Host service agent
│   ├── ShieldChecker.HostService/
│   └── ShieldChecker.HostService.Core/
├── Webapp/                             # Web application frontend
│   └── ShieldChecker.WebApp/
└── Import-AtomicRedTeamToSharedLibrary.ps1  # Atomic Red Team import script
```

## Documentation

- [Homepage](https://www.shieldchecker.ch)
- [Deployment Guide](docs/Deployment.md) - Detailed deployment instructions
- [Documentation](docs/Documentation.md) - Comprehensive project documentation

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Build and verify your changes locally
5. Submit a pull request

Please see our [issue templates](.github/ISSUE_TEMPLATE/) for bug reports and feature requests.

## Support

For issues and support:
- Check existing [GitHub Issues](https://github.com/ThomasKur/UseCase.ShieldChecker/issues)
- Review the [Documentation](/docs/Documentation.md)
- Consult the [Deployment Guide](/docs/Deployment.md)

There is no support or guaranteed answer. The project is a community project and maintained as a hobby.

## License

This project is licensed under the terms specified in the [LICENSE](LICENSE) file.
