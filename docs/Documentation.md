
# ShieldChecker Documentation

Welcome to the documentation for ShieldChecker, an open-source security testing platform designed to validate Microsoft Defender XDR detections through real-world test execution.

## Quick Start Guide

New to ShieldChecker? Start here:

1. **[Deployment Guide](Deployment.md)** - Complete setup and installation instructions
2. **[First Run Wizard](Deployment.md#post-deployment-configuration)** - Initial configuration walkthrough
3. **[Test Management](CreateTests.md)** - Create your first security test
4. **[Run Tests](RunAndScheduleTests.md)** - Execute and monitor test results

## Complete Documentation Library

### 📋 Getting Started
- **[README](../README.md)** - Project overview and quick introduction
- **[Deployment Guide](Deployment.md)** - Comprehensive deployment instructions
  - Prerequisites and environment setup
  - Step-by-step deployment process
  - First Run Wizard configuration
  - Troubleshooting and maintenance

### 🧪 Test Management
- **[Test Creation and Management](CreateTests.md)** - Complete test lifecycle management
  - Creating new security tests
  - Test configuration options
  - MITRE ATT&CK mapping
  - Version history and restoration
  - Best practices for test development

### ⚡ Test Execution
- **[Run and Schedule Tests](RunAndScheduleTests.md)** - Test execution and automation
  - Single test execution
  - Automated scheduling configuration
  - Job monitoring and management
  - Review Mode for troubleshooting
  - Performance optimization

### 📊 Reporting and Analytics
- **[Reporting Guide](Reporting.md)** - Comprehensive reporting capabilities
  - Dashboard overview and insights
  - Detection coverage analysis
  - Cost monitoring and optimization
  - Advanced reporting with Power BI
  - Custom report creation

### 🔧 Legacy Documentation
- **[Manage Tests](ManageTests.md)** - *Note: Replaced by [CreateTests.md](CreateTests.md)*
- **[Manage Jobs](ManageJobs.md)** - *Note: Content integrated into [RunAndScheduleTests.md](RunAndScheduleTests.md)*

## Architecture Components

| Component | Purpose | Documentation |
|-----------|---------|---------------|
| **Web Application** | Management interface and reporting | [Reporting Guide](Reporting.md) |
| **Backend API** | Internal API consumed by the web application | [Deployment Guide](Deployment.md) |
| **HostService API** | External API for host service communication | [Deployment Guide](Deployment.md) |
| **Import API** | External API for importing shared library tests | [Deployment Guide](Deployment.md) |
| **Data Access** | Shared EF Core models and database context | [Deployment Guide](Deployment.md) |
| **Host Service** | Agent running on test machines | [Deployment Guide](Deployment.md) |
| **Bicep Templates** | Infrastructure as Code deployment | [Deployment Guide](Deployment.md) |


## Additional Resources

- **Release Notes:** See [GitHub Releases](https://github.com/ThomasKur/UseCase.ShieldChecker/releases) for version history

### External Links
- **[Project Homepage](https://www.shieldchecker.ch)** - Latest news, updates, and community information
- **[MITRE ATT&CK Framework](https://attack.mitre.org/)** - Reference for attack technique mapping
- **[Atomic Red Team](https://github.com/redcanaryco/atomic-red-team)** - Open-source testing framework integration
- **[Microsoft Defender XDR](https://docs.microsoft.com/en-us/microsoft-365/security/defender/)** - Official Microsoft documentation

## Getting Help - Community Support
- [GitHub Issues](https://github.com/ThomasKur/UseCase.ShieldChecker/issues) - Report bugs and request features
- [Project Homepage](https://www.shieldchecker.ch) - Latest news and updates

> **Note:** ShieldChecker is a community-driven project maintained as a hobby. While we strive to help, there are no guaranteed response times or support SLAs.

---

**Ready to get started?** Begin with the [Deployment Guide](Deployment.md) to set up your ShieldChecker environment.

