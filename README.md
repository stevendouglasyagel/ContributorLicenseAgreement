# Contributor License Agreement - CLA

## What is CLA?

CLA is a tool that allows outside contributors to sign a contribution license agreement (cla).
With this agreement signed, external contributors can contribute code to Microsoft open-source repos.

## Usage

To use CLA, you need to define a [cla.yml](src/ContributorLicenseAgreement.Core.Tests/Data/cla.yml) file on org level.
This YAML file defines how the CLA should act, what the agreement should be, and which accounts are exempt from signing.
Furthermore, the *Microsoft GitHub Policy Service* needs to be installed for your organization.

### cla.yml - required properties
- **files**: defines the minimum number of files changed for cla to act.
- **codeLines**: defines the minimum number of code lines changed for cla to act.
- **claContent**: the contribution licence agreement the author should sign.

### cla.yml - optional properties
- **bypassUsers**: defines the users for which the cla check is omitted.
- **prohibitedCompanies**: defines the companies for which users cannot sign a cla.
- **autoSignMsftEmployee**: if set to true, Microsoft employees will not be asked to sign a cla.

## Commands

Whenever a pull request is created, CLA will check whether or not to user who opened the pr has 
already signed an agreement. If not, it will output a comment prompting the user to accept the agreement.

### Accepting

To accept the agreement, the user can issue one of the following two commands.

```
@bot aggree
@bot aggree company="your company"
```

### Terminating

A user can choose to terminate the signed agreement by issuing the following command under a pull
request that was opened by the same user.

```
@bot terminate
```


## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
