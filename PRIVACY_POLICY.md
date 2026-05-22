# FocusTray Privacy Policy

### 1. General Information

FocusTray is a Windows desktop application designed to enhance productivity through focus session management and optional JIRA integration. The application operates entirely locally on your computer and does not transmit any personal data to the application developer.

**Last Updated**: May 22, 2026  
**Version**: 1.0

**Developer**: Marcin Świątnicki (kubis1982)  
**Contact**: https://github.com/kubis1982/FocusTray/issues

### 2. What Data We Collect and How We Use It

#### 2.1. Locally Stored Data

FocusTray stores the following data exclusively on your computer:

**a) Application Settings** (`%LocalApplicationData%\FocusTray\settings.json`):
- JIRA company name (e.g., "yourcompany" for yourcompany.atlassian.net)
- JQL filter for JIRA queries
- Microsoft Teams integration settings (if configured)
- **Note**: Passwords and tokens are NOT stored in this file

**b) Authentication Credentials** (`%LocalApplicationData%\FocusTray\credentials\`):
- JIRA user email
- JIRA API token
- Data is encrypted using Windows Data Protection API (DPAPI)
- Per-user encryption - only you have access to this data
- Stored locally, never sent to the developer

**c) Application Logs** (`%LocalApplicationData%\FocusTray\logs\`):
- Diagnostic information about application operation
- Error and exception data
- Does not contain passwords or access tokens
- Stored locally for debugging purposes

**d) Focus Sessions**:
- Task descriptions entered by the user
- Session duration
- JIRA issue associations (if used)
- Data stored exclusively locally

#### 2.2. Data Transmitted to External Systems

**JIRA (Atlassian Cloud)** - optional, only when user configures integration:
- Email and JIRA API token are used to authenticate with JIRA API
- Data transmitted directly from your computer to Atlassian servers
- Queries for assigned issues (according to JQL filter)
- Creation of worklog entries (time tracking) in JIRA issues
- **The application developer has NO access to this data**
- Data transmission governed by [Atlassian Privacy Policy](https://www.atlassian.com/legal/privacy-policy)

**Microsoft Teams** - optional, only when user configures integration:
- Microsoft Identity Platform authentication token
- User presence status information
- Data transmitted directly from your computer to Microsoft servers
- **The application developer has NO access to this data**
- Data transmission governed by [Microsoft Privacy Policy](https://privacy.microsoft.com/privacystatement)

### 3. Data Security

#### 3.1. Encryption
- All sensitive authentication data is encrypted using Windows Data Protection API (DPAPI)
- Per-user encryption - data encrypted by your Windows account
- Only you (logged into this Windows account) can decrypt the data

#### 3.2. Local Storage
- All data is stored exclusively on your computer
- No data is transmitted to FocusTray developers
- No data is stored in the cloud by FocusTray

#### 3.3. Data Access
- **The application developer has no access to your data**
- Data is accessible only to you on your local computer
- External integrations (JIRA, Teams) work directly from your computer

### 4. Third-Party Data Sharing

FocusTray **does not share, sell, or transfer** your personal data to any third parties, with the following exceptions:

- **Atlassian (JIRA)**: If you configure JIRA integration, authentication credentials and worklog data are transmitted directly to Atlassian servers according to their privacy policy
- **Microsoft (Teams)**: If you configure Teams integration, authentication token is transmitted directly to Microsoft servers according to their privacy policy

**The FocusTray developer has no access to your data in these systems.**

### 5. Your Rights

You have full control over your data:

#### 5.1. Data Access
All data is stored locally in directories:
- `%LocalApplicationData%\FocusTray\settings.json` - settings
- `%LocalApplicationData%\FocusTray\credentials\` - authentication credentials (encrypted)
- `%LocalApplicationData%\FocusTray\logs\` - application logs

#### 5.2. Data Deletion
You can delete your data by:
- Uninstalling the application through Windows Settings → Apps
- Manually deleting the `%LocalApplicationData%\FocusTray\` directory
- Using the "Logout" function in the application (removes authentication credentials)

#### 5.3. Data Export
You can copy files from the `%LocalApplicationData%\FocusTray\` directory at any time

### 6. Children's Data

FocusTray is not intended for individuals under 16 years of age. We do not knowingly collect personal data from children.

### 7. Changes to Privacy Policy

We will inform users of significant changes to this privacy policy through:
- Updating the version number and date in this document
- Information in release notes on GitHub
- Optional in-application notification

### 8. Contact

For questions regarding this privacy policy, contact us:
- **GitHub Issues**: https://github.com/kubis1982/FocusTray/issues
- **Email**: marcin.swiatnicki@gmail.com

### 9. Legal Basis (GDPR)

If you are a user from the European Union, we process your personal data on the following legal bases:
- **Consent**: By configuring integration with JIRA or Teams, you consent to processing authentication data
- **Legitimate Interest**: Processing local data to provide application services

### 10. GDPR Compliance

FocusTray is designed with user privacy in mind (privacy by design):
- Data minimization - we collect only necessary information
- Local storage - all data remains on your computer
- Encryption - sensitive data is encrypted
- User control - full control over data and ability to delete it

---

## Key Points Summary

### 🔒 Security
- ✅ Data encrypted using Windows DPAPI
- ✅ Exclusively local storage
- ✅ Developer has no access to your data

### 🌐 External Integrations
- ✅ JIRA - optional, direct connection to Atlassian
- ✅ Teams - optional, direct connection to Microsoft
- ✅ No intermediaries - data doesn't go through developer's servers

### 🛡️ Your Rights
- ✅ Full control over your data
- ✅ Ability to delete at any time
- ✅ Access to all stored data

### 📋 Compliance
- ✅ GDPR compliant
- ✅ Privacy by design
- ✅ Data minimization principle

---

**License**: MIT License  
**Source Code**: https://github.com/kubis1982/FocusTray  
**Project Website**: https://github.com/kubis1982/FocusTray
