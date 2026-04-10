using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Security.Claims;

namespace ShieldChecker.WebApp.Models.Db
{
    public class UserInfo
    {
        public UserInfo() { }
        public UserInfo(string? displayName, string? userPrincipalName, Guid id)
        {
            DisplayName = displayName;
            UserPrincipalName = userPrincipalName;
            Id = id;
        }
        public UserInfo(ClaimsPrincipal User)
        {
            DisplayName = User.Claims.Where(c => c.Type == "name").First().Value;

            if (User.Identity != null) { 
                UserPrincipalName = User.Identity?.Name; 
            } else
            {
                UserPrincipalName = "Unknown UPN";
            }
            Id = new Guid(User.Claims.Where(c => c.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier").First().Value);
        }

        public string? DisplayName { get; set; }
        public string? UserPrincipalName { get; set; }
        public Guid Id { get; set; }
        public static UserInfo EnsureUserInDb(ClaimsPrincipal User, ShieldCheckerContext context)
        {
            Guid Id = new Guid(User.Claims.Where(c => c.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier").First().Value);

            UserInfo foundUsr = context.UserInfo.Where(u => u.Id == Id).FirstOrDefault();
            if (foundUsr == null)
            {
                foundUsr = new UserInfo(User);
                context.UserInfo.Add(foundUsr);
                context.SaveChanges();
                return foundUsr;
            }
            else
            {
                if (foundUsr.DisplayName != User.Claims.Where(c => c.Type == "name").First().Value)
                {
                    foundUsr.DisplayName = User.Claims.Where(c => c.Type == "name").First().Value;
                    context.SaveChanges();
                }
                if (foundUsr.UserPrincipalName != User.Identity?.Name) // Fixed potential null reference
                {
                    foundUsr.UserPrincipalName = User.Identity?.Name;
                    context.SaveChanges();
                }
                return foundUsr;
            }
        }

    }
}
