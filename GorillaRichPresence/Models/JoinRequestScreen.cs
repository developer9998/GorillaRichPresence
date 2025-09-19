using Discord;
using GorillaInfoWatch.Models;
using GorillaInfoWatch.Models.Widgets;
using System;
using System.Linq;

namespace GorillaRichPresence.Models
{
    internal class JoinRequestScreen : InfoScreen
    {
        public override string Title => "Join Request";

        public static bool hasUser;

        public static User requestingUser;

        public static UnityEngine.Texture requestingAvatar;

        public static Action<User, ActivityJoinRequestReply> sendReply;

        public override InfoContent GetContent()
        {
            if (!hasUser)
            {
                ReturnScreen();
                return null;
            }

            LineBuilder lines = new();

            lines.Add("A user has requested to join you:");
            lines.Add(requestingUser.Username, new Widget_Symbol((Symbol)requestingAvatar));

            lines.Skip();

            lines.Add("Accept", new Widget_PushButton(ReplyChosen, ActivityJoinRequestReply.Yes));
            lines.Add("Request will advance to inviting the user to play");
            lines.Skip();

            lines.Add("Decline", new Widget_PushButton(ReplyChosen, ActivityJoinRequestReply.No));
            lines.Add("Request is explicitly denied, user may be informed");
            lines.Skip();

            lines.Add("Ignore", new Widget_PushButton(ReplyChosen, ActivityJoinRequestReply.Ignore));
            lines.Add("Request will remain active, no response is sent");

            return lines;
        }

        public void ReplyChosen(object[] args)
        {
            if (args.ElementAtOrDefault(0) is ActivityJoinRequestReply reply)
                ReplyChosen(reply);
        }

        public void ReplyChosen(ActivityJoinRequestReply reply)
        {
            if (hasUser)
            {
                sendReply?.Invoke(requestingUser, reply);
                hasUser = false;
                ReturnScreen();
            }
        }
    }
}
