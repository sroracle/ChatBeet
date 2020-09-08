using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ChatBeet.Models;
using ChatBeet.Services;
using GravyBot;
using GravyIrc.Messages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBeet.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly MessageQueueService messageQueue;
        private readonly UserManager<IdentityUser> userManager;
        private static readonly string TokenAction = "passwordless-auth";
        private static readonly string TokenProvider = "Default";

        public LoginModel(MessageQueueService messageQueue, UserManager<IdentityUser> userManager)
        {
            this.messageQueue = messageQueue;
            this.userManager = userManager;
        }

        [BindProperty]
        public LoginTokenRequest LoginInfo { get; set; }

        [BindProperty]
        public string ReturnUrl { get; set; }

        public string ValidationMessage { get; set; }

        public void OnGet(string ReturnUrl = default)
        {
            this.ReturnUrl = ReturnUrl;
        }

        [HttpPost]
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                LoginInfo = null;
                return Page();
            }

            var user = await userManager.FindByNameAsync(LoginInfo.Nick);
            if (string.IsNullOrEmpty(LoginInfo.AuthToken))
            {
                // send token
                if (user == default)
                {
                    user = new IdentityUser(LoginInfo.Nick) { UserName = LoginInfo.Nick };
                    await userManager.CreateAsync(user);
                }

                var token = await userManager.GenerateUserTokenAsync(user, TokenProvider, TokenAction);

                messageQueue.Push(new LoginTokenRequest { Nick = LoginInfo.Nick, AuthToken = token });
            }
            else
            {
                // handle login
                if (user == default)
                {
                    ValidationMessage = "User not found.  Try getting a fresh token.";
                    LoginInfo = null;
                    return Page();
                }

                var isValid = await userManager.VerifyUserTokenAsync(user, TokenProvider, TokenAction, LoginInfo.AuthToken);
                if (isValid)
                {
                    await userManager.UpdateSecurityStampAsync(user);
                    var claims = new ClaimsIdentity(new List<Claim> {
                        new Claim("sub", user.Id),
                        new Claim("nick", LoginInfo.Nick),
                        new Claim(ClaimTypes.NameIdentifier, LoginInfo.Nick)
                    }, IdentityConstants.ApplicationScheme);
                    await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, new ClaimsPrincipal(claims));
                    return RedirectToPage(string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "/Account/Success");
                }
                else
                {
                    ValidationMessage = "The provided authentication token was invalid.";
                    LoginInfo = null;
                    return Page();
                }
            }

            return Page();
        }
    }
}
