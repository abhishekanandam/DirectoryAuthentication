using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.Protocols;

public partial class _Default : System.Web.UI.Page
{
    
    protected void btnAuthenticate_Click(object sender, EventArgs e)
    {

        string strDomain = string.Empty;

        string strUserName = txtUserName.Text;
        string strPassword = txtPassword.Text;

        lblAuthMessage2_0.Text = String.Empty;
        lblAuthMessage3_5.Text = String.Empty;

        try
        {
            if (strUserName == string.Empty)
                throw new AuthenticationException("User name is blank!!");

            if (strPassword == string.Empty)
                throw new AuthenticationException("Password is blank!!");

            {
                string strUser = System.Environment.UserName;
                string strDin = System.Environment.UserDomainName;
            }


            if (strUserName.Contains("@"))
            {
                string[] strArr = strUserName.Split('@');
                if (strArr.Length != 2)
                    throw new AuthenticationException("Please enter valid user name with domain.");
                strUserName = strArr[0];
                strDomain = strArr[1];
            }
            else
                throw new AuthenticationException("Please provide FQDN.");

            string strDistinguishedName = string.Empty;
            string strMessage = String.Empty;

            try
            {
                // Authenticate using Principalcontext.
                strDistinguishedName = AuthenticateUsingPrincipalcontext(strDomain, strUserName, strPassword);

                strMessage = String.Format("User '{0}' authenticated successfully with PrincipalContext, \nDistinguished Name: {1}", strUserName, strDistinguishedName);
                lblAuthMessage3_5.ForeColor = System.Drawing.Color.Green;
                lblAuthMessage3_5.Text = strMessage;
            }
            catch (AuthenticationException ex)
            {
                strMessage = String.Format("Error: {0}", ex.Message);
                lblAuthMessage3_5.ForeColor = System.Drawing.Color.Red;
                lblAuthMessage3_5.Text = strMessage;
            }

            try
            {
                // Authenticate using Principalcontext.
                strDistinguishedName = AuthenticateUsingDirectoryEntry(strDomain, strUserName, strPassword);

                strMessage = String.Format("User '{0}' authenticated successfully with DirectoryEntry, \nDistinguished Name: {1}", strUserName, strDistinguishedName);
                lblAuthMessage2_0.ForeColor = System.Drawing.Color.Green;
                lblAuthMessage2_0.Text = strMessage;

            }
            catch (AuthenticationException ex)
            {
                strMessage = String.Format("Error: {0}", ex.Message);
                lblAuthMessage2_0.ForeColor = System.Drawing.Color.Red;
                lblAuthMessage2_0.Text = strMessage;


            }
        }
        catch (Exception ex)
        {
            string strMessage = String.Format("Unknown error occured in Authentication.\nError: {0}", ex.Message);
            lblAuthMessage2_0.ForeColor = System.Drawing.Color.Red;
            lblAuthMessage2_0.Text = strMessage;
        }
        txtUserName.Text = string.Empty;
        txtPassword.Text = string.Empty;

        txtUserName.Focus();
    }

    /*
     * Authenticate using Principalcontext
     */
    
    private string AuthenticateUsingPrincipalcontext( string strDomain, string strUserName, string strPassword)
    {
        string strDistinguishedName = string.Empty;

        PrincipalContext ctx = new PrincipalContext(ContextType.Domain, strDomain);

        try
        {
            bool bValid = ctx.ValidateCredentials(strUserName, strPassword);

            // Additional check to search user in directory.
            if (bValid)
            {
                UserPrincipal prUsr = new UserPrincipal(ctx);
                prUsr.SamAccountName = strUserName;

                PrincipalSearcher srchUser = new PrincipalSearcher(prUsr);
                UserPrincipal foundUsr = srchUser.FindOne() as UserPrincipal;

                if (foundUsr != null)
                {
                    strDistinguishedName = foundUsr.DistinguishedName;

                }
                else
                    throw new AuthenticationException("Please enter valid UserName/Password.");
            }
            else
                throw new AuthenticationException("Please enter valid UserName/Password.");

        }catch(Exception ex)
        {
            throw new AuthenticationException("Authentication Error in PrincipalContext. Message: " + ex.Message);
        }
        finally
        {
            ctx.Dispose();
        }

        return strDistinguishedName;

    }


    /*
     * Authenticate using DirectoryEntry .NET 2.0
     */

    private string AuthenticateUsingDirectoryEntry(string strDomain, string strUserName, string strPassword)
    {
        string strDistinguishedName = string.Empty;

        try{
            // Temporarily created the path, there are various ways by which we can get the path.
            string strPath = "LDAP://";
            string[] domainArr = strDomain.Split('.');

            foreach (string strDC in domainArr)
            {
                strPath += string.Format("DC={0},", strDC);
            }

            if (strPath.EndsWith(","))
                strPath = strPath.Substring(0, strPath.Length - 1);

             DirectoryEntry dirEntry = new DirectoryEntry(strPath);

            // Additional facility, this should not be required anyway. We can Fetch default naming context of current user domain by below code.
            if (dirEntry == null)
            {
                string strDN = string.Empty;
                dirEntry = new DirectoryEntry("LDAP://RootDSE");

                using (dirEntry)
                {
                    strDN = dirEntry.Properties["defaultNamingContext"][0].ToString();
                    dirEntry.Dispose();
                }
                dirEntry = new DirectoryEntry("LDAP://" + strDN);
            }

            if (dirEntry == null)
                throw new AuthenticationException("DirectoryEntry object cannot be instantiated.");

            DirectoryEntry userEntry = new DirectoryEntry(dirEntry.Path, strUserName, strPassword);

            try{
                // This varifies the user with Active Directory and if user is not valid then exception is thrown.
                object obj = userEntry.NativeObject;
            }catch(Exception ex)
            {
                // Given user/password not valid. This happens if the impersonating user is in the different domain of the providd user
                // We need an LDAP authenticatino in this case.
                string strLink = "WinNT://" + strDomain.ToLower() + "/Domain Controllers";
                DirectoryEntry _tempEntry = new DirectoryEntry(strLink);
                object  DomainControllers = _tempEntry.Invoke("members", null);
                foreach (object dc in (System.Collections.IEnumerable) DomainControllers)
                { 
                    DirectoryEntry dcEntry = new DirectoryEntry(dc);
                    string strDNS = dcEntry.Name;
                    if (strDNS.EndsWith("$")) 
                        strDNS = strDNS.Substring(0, strDNS.Length - 1);

                    try {  
                        // If Bind fails, the user might not be available or the password is in valid.  
                        LdapConnection ldapConn = new LdapConnection(strDNS);
                        ldapConn.Credential = new System.Net.NetworkCredential(strUserName, strPassword);
                        ldapConn.Bind();  
                        break;
                        // TODO: might not be correct. Was : Exit For 
                    } 
                    catch (Exception exp)
                    {
                        //Failed on LDAP as well. User/Pwd not valid.
                        throw new AuthenticationException("Can't find user in LDAP." + exp.Message);
                    }
                }                        

            }

            // We are here it means, eaither our directoryEntry or LDAP has authenticated the user. 
            // Do additional check to search the DistinguishedName
            DirectorySearcher search = new DirectorySearcher(dirEntry);
            search.Filter = "(SAMAccountName=" + strUserName + ")";
            search.PropertiesToLoad.Add("distinguishedname");
            search.ReferralChasing = ReferralChasingOption.All;
            SearchResult result = search.FindOne();
            if (result == null)
                throw new Exception("Can't find user in LDAP");
            
            strDistinguishedName = result.Properties["distinguishedname"][0].ToString();

        }
        catch (Exception ex)
        {
            throw new AuthenticationException("Authentication Error in DirectoryEntry. Message: " + ex.Message);
        }
        finally
        {

        }

        return strDistinguishedName;
    }

}

public class AuthenticationException : Exception
{
    public AuthenticationException()
    {
    }

    public AuthenticationException(string msg)
        : base(msg)
    {
    }
}