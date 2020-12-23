using LdapForNet;
using System;
using System.Collections.Generic;
using System.Linq;
using static LdapForNet.Native.Native;

namespace test
{
    class Program
    {
        static void Main(string[] args)
        
        {
            Console.Write("host:");
            string host = Console.ReadLine();
            Console.Write("login:");
            string user = Console.ReadLine();
            Console.Write("password:");

            var password = string.Empty;
            ConsoleKey key;
            do
            {
                var keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.Key;

                if (key == ConsoleKey.Backspace && password.Length > 0)
                {
                    Console.Write("\b \b");
                    password = password[0..^1];
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    Console.Write("*");
                    password += keyInfo.KeyChar;
                }
            } while (key != ConsoleKey.Enter);
            
            try
            {
                using var connection = new LdapConnection();

                //connection.Connect(new Uri($"ldaps://{host}:636"));
                connection.Connect(host, 636, LdapSchema.LDAPS);
                //connection.Connect("hq.arzinger.local", 636, LdapSchema.LDAPS);

                connection.TrustAllCertificates();
                connection.SetOption(LdapOption.LDAP_OPT_REFERRALS, 0);

                connection.Bind(LdapAuthType.Simple, new LdapCredential { UserName = $@"{GetFirstDnFromHost(host)}\{user}", Password = password });
                //connection.Bind(LdapAuthType.Digest, new LdapCredential { UserName = user, Password = password });

                var dn = GetDnFromHost(host);
                var filter = "(&(objectCategory=group))";
                var pageResultRequestControl = new PageResultRequestControl(500) { IsCritical = true };
                var searchRequest = new SearchRequest(dn, filter, LdapSearchScope.LDAP_SCOPE_SUBTREE)
                {
                    AttributesOnly = false,
                    TimeLimit = TimeSpan.Zero,
                    Controls = { pageResultRequestControl }
                };

                var entries = new List<DirectoryEntry>();

                while (true)
                {
                    var response = (SearchResponse)connection.SendRequest(searchRequest);

                    foreach (var control in response.Controls)
                    {
                        if (control is PageResultResponseControl control1)
                        {
                            // Update the cookie for next set
                            pageResultRequestControl.Cookie = control1.Cookie;
                            break;
                        }
                    }

                    // Add them to our collection
                    entries.AddRange(response.Entries);

                    // Our exit condition is when our cookie is empty
                    if (pageResultRequestControl.Cookie.Length == 0)
                        break;
                }

                Console.WriteLine("Groups count: " + entries.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            Console.WriteLine("--------------");
            Console.WriteLine("Finish");
            Console.ReadKey();
        }

        static string GetDnFromHost(string hostname)
        {
            char separator = '.';
            var parts = hostname.Split(separator);
            var dnParts = parts.Select(_ => $"dc={_}");
            return string.Join(",", dnParts);
        }

        static string GetFirstDnFromHost(string hostname)
        {
            char separator = '.';
            var parts = hostname.Split(separator);
            return parts[0];
        }

    }
}
