# ShieldChecker Deployment Guide

This comprehensive guide walks you through deploying ShieldChecker, a security testing platform that validates Microsoft Defender XDR detections through real-world test execution.

## Prerequisites

Before beginning the deployment, ensure you have the following requirements:

### Software Requirements
- **Azure PowerShell Module** (latest version)
- **PowerShell Core 7+** 
- **Azure CLI** (latest version)
- **Bicep CLI** (latest version)

### Azure Environment Requirements
- **Dedicated Test Tenant** (strongly recommended for production isolation)
- **Microsoft 365 E5/A5 License** (provides Microsoft Defender XDR capabilities)
- **Azure Subscription** with sufficient permissions
- **Global Administrator** or equivalent permissions for initial setup

### Hardware Considerations
- Estimated monthly cost: ~$200 USD (Azure pay-as-you-go pricing)
- CPU core availability for parallel worker VMs
- Storage requirements for test artifacts and logs

## Pre-Deployment Setup

### Step 1: Create Database Admin Group in Entra ID

1. **Navigate to Entra ID** in the Azure portal
2. **Create a new security group:**
   - Name: `sg-ShieldChecker-DB-Admins` (or your preferred naming convention)
   - Type: Security
   - Membership type: Assigned
3. **Add members to the group:**
   - Add your user account for manual deployments
   - For CI/CD pipelines, add the Service Principal instead
4. **Document the following for deployment:**
   - Group name

### Step 2: Verify Software Installation

Confirm all required tools are properly installed:

```powershell
# Verify PowerShell version (should be 7.0+)
$PSVersionTable.PSVersion

```


## Deployment Process

### Step 1: Download and Extract Release

1. **Download the latest version** from the [GitHub Releases page](https://github.com/ThomasKur/UseCase.ShieldChecker/releases)
2. **Extract the archive** to your preferred deployment directory
3. **Open PowerShell Core** and navigate to the extracted folder:

```powershell
cd "C:\Path\To\Extracted\ShieldChecker"
```

### Step 2: Azure Authentication

Authenticate to Azure and select the target subscription:

```powershell
# Connect to Azure (will open browser for authentication)
Connect-AzAccount

# List available subscriptions
Get-AzSubscription

# Select the target subscription
Select-AzSubscription -SubscriptionId "<YourSubscriptionId>"

# Verify current context
Get-AzContext
```

### Step 3: Execute Initial Deployment

Run the deployment script with your environment-specific parameters:

```powershell
./Invoke-Deploy.ps1 -SQLAdminGroupName "<SQLAdminGroupName>" `
                    -ApplicationName "<ApplicationName>" `
                    -ResourceGroupName "<ResourceGroupName>" `
                    -adminPw (Read-Host -Prompt "Enter Admin Password" -AsSecureString) `
                    -CreateRessourceGroupIfNotExistsLocation "<AzureRegion>"
```

#### Parameter Descriptions

| Parameter | Description | Example |
|-----------|-------------|---------|
| `SQLAdminGroupName` | Entra ID group name for database administration | `sg-ShieldChecker-DB-Admins` |
| `ApplicationName` | Unique name for the application (used in resource naming) | `sc-prod` |
| `ResourceGroupName` | Azure resource group name (created if doesn't exist) | `rg-shieldchecker-prod` |
| `adminPw` | Secure password for administrative accounts | *Prompted securely* |
| `CreateRessourceGroupIfNotExistsLocation` | Azure region for deployment | `East US`, `West Europe`, ... |

#### Example

```powershell
./Invoke-Deploy.ps1 -SQLAdminGroupName  "sg-ShieldChecker-DB-Admins" `
                    -ApplicationName "sc-prod" `
                    -ResourceGroupName "rg-shieldchecker-prod" `
                    -adminPw (Read-Host -Prompt "Enter Admin Password" -AsSecureString) `
                    -CreateRessourceGroupIfNotExistsLocation "westeurope"
```

#### Deployment Timeline

- **Expected Duration:** 15-30 minutes
- **Progress Monitoring:** Watch PowerShell output for deployment status
- **Completion Indicator:** "Deployment script completed successfully" message

> **Important:** Document all deployment parameters for future updates and maintenance.

## Post-Deployment Configuration

### Accessing the Application

After successful deployment, access your ShieldChecker instance:

1. **Locate the Web App URL** in the deployment output
2. **Navigate to the application** in your browser
3. **Start the First Run Wizard** to complete the setup

### First Run Wizard

The First Run Wizard guides you through the essential configuration steps required for ShieldChecker operation.

#### Step 1: Welcome and System Overview

![First Run Wizard - Welcome](img/ShieldChecker-First-run-Wizard-01.png)

- **Review the setup overview** and system requirements
- **Verify deployment status** of core components
- **Click "Start Wizard"** to begin configuration

#### Step 2: Microsoft Defender for Endpoint (MDE) Configuration

![First Run Wizard - MDE Setup](img/ShieldChecker-First-run-Wizard-02.png)
![First Run Wizard - MDE Configuration Details](img/ShieldChecker-First-run-Wizard-03.png)

**Critical Configuration Step:** To enable automatic MDE onboarding for worker devices, you must provide onboarding scripts from your Microsoft 365 Defender portal.

**How to obtain MDE onboarding scripts:**
1. Navigate to **Microsoft 365 Defender portal** (security.microsoft.com)
2. Go to **Settings > Endpoints > Device management > Onboarding**
3. Select **Windows** as the operating system
4. Choose **VDI Onboarding Script** as the deployment method
5. **Download the onboarding package**
6. **Extract and copy the script content** into the ShieldChecker configuration
7. Repeat step 3-6 for **Linux** and **Local Script**.

**Script Format Verification:** Ensure your scripts match the format shown in the screenshots above.

#### Step 3: Permission Validation

![First Run Wizard - Permission Setup](img/ShieldChecker-First-run-Wizard-04.png)

**Automatic Permission Verification:** This step validates that all required Azure permissions were granted correctly during deployment. If missing then a script is provided for fixing the problem.

**Common Permission Requirements:**
- Microsoft Graph API permissions for Entra ID integration
- Azure Resource Manager permissions for VM management
- Microsoft Defender for Endpoint API access

#### Step 4: Test Import and System Status

![First Run Wizard - Test Import and System Status](img/ShieldChecker-First-run-Wizard-05.png)

**Test Framework Integration:** Import pre-built security tests to quickly start validation.

**Available Test Sources:**
- **Atomic Red Team:** Comprehensive collection of security tests mapped to MITRE ATT&CK
- **Other Test Libraries:** Other libraries will be integrated in the future.

**Domain Controller Status Monitoring:**
- **Initializing:** Domain controller deployment in progress
- **Initialized:** Ready for test execution
- **Error:** Review logs and troubleshoot deployment issues

**Next Steps:** Once the Domain Controller status shows "Initialized" and you have imported or [created tests](/docs/ManageTests.md), you can [begin executing](/docs/ManageJobs.md) security validations.

### Completing the Setup

After the First Run Wizard completes:

1. **Verify all components are operational on the homepage/dashboard**
2. **Create or import your first security tests**
3. **Execute a test run to validate the platform**
4. **Review the generated reports and detections**

For detailed guidance on test creation and execution, refer to:
- [Test Management Guide](ManageTests.md)
- [Run and Schedule Tests Guide](ManageJobs.md)

## Maintenance and Updates

### Updating Existing Deployments

For incremental updates, re-run `Invoke-Deploy.ps1` with `-Skip*` switches to target only the components that changed:

```powershell
# Container-image-only update (skip infra + SQL)
./Invoke-Deploy.ps1 -SQLAdminGroupName "<SQLAdminGroupName>" `
                    -ResourceGroupName "<ResourceGroupName>" `
                    -WebAppImageTag "2.1.0" `
                    -SkipAppRegistration -SkipBicep -SkipSqlSetup -SkipHostServicePackage
```

**Available `-Skip*` switches:**

| Switch | Effect |
|--------|--------|
| `-SkipAppRegistration` | Skip creating / updating Entra ID App Registrations |
| `-SkipBicep` | Skip Bicep infrastructure deployment |
| `-SkipSqlSetup` | Skip SQL schema, permissions and initialisation |
| `-SkipDockerBuild` | Skip the Docker build step (assumes images already exist locally) |
| `-SkipDockerPush` | Skip pushing Docker images to ACR |
| `-SkipHostServicePackage` | Skip building and packaging the HostService zip |

### Backup and Recovery

**Automated Backups:**
- SQL Database: Backups can be enabled
- Test Definitions: Backed up with database

### Monitoring and Troubleshooting

**Health Monitoring:**
- Azure Application Insights integration
- Built-in health checks in the web application
- Domain Controller status monitoring ind dashboard

**Common Issues and Solutions:**

| Issue | Symptoms | Resolution |
|-------|----------|------------|
| Domain Controller not initializing | Status remains "Initializing" | Check Azure VM deployment logs, and logs provided in the ShieldChecker UI |
| MDE onboarding failures | Worker VMs not appearing in Defender portal | Verify onboarding script configuration, check network connectivity |

## Support and Additional Resources

### Documentation Links
- [Contents](Documentation.md) - Documentation Index

### Getting Help - Community Support
- [GitHub Issues](https://github.com/ThomasKur/UseCase.ShieldChecker/issues) - Report bugs and request features
- [Project Homepage](https://www.shieldchecker.ch) - Latest news and updates

> **Note:** ShieldChecker is a community-driven project maintained as a hobby. While we strive to help, there are no guaranteed response times or support SLAs.

