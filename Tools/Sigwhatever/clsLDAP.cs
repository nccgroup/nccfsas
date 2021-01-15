using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;

namespace Sigwhatever
{
    class ClsLDAP
    {
        public List<string> EnumGroupEmails(string groupName, string domainName)
        {
            List<string> emails = new List<string>();
            try
            {
                using (PrincipalContext ctx = new PrincipalContext(ContextType.Domain, domainName))
                {
                    using (GroupPrincipal grp = GroupPrincipal.FindByIdentity(ctx, IdentityType.Name, groupName))
                    {
                        var sams = from x in grp.GetMembers(true) select new { x.SamAccountName, };
                        var users = from sam in sams.Distinct()
                                    let usr = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, sam.SamAccountName)
                                    select new { usr.SamAccountName, usr.DisplayName, usr.EmailAddress };
                        
                        foreach (var u in users)
                        {
                            if (u.EmailAddress != null)
                            {
                                Console.WriteLine("Adding " + u.DisplayName + ": " + u.EmailAddress);
                                emails.Add(u.EmailAddress);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error getting emails: " + e.Message);
            }
            return emails;
        }
    }
}
