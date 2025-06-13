using Discord;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace GorillaRichPresence.Tools
{
    public static class DiscordWrapper
    {
        public static event Action<string> OnActivityJoin;

        private static Discord.Discord discord;
        private static ActivityManager activityManager;
        private static Activity activity;

        private static bool toUpload = true;
        private static float lastUploadTime = Constants.ActivityUploadDelay;

        public static void Construct()
        {
            new Thread(RegisterDiscord).Start();
        }

        public static void RegisterDiscord()
        {
            if (discord != null)
            {
                Logging.Error("RegisterDiscord was cancelled as a Discord instance is already in use.");
                return;
            }

            discord = new Discord.Discord(Constants.ApplicationID, (ulong)CreateFlags.NoRequireDiscord);

            discord.SetLogHook(LogLevel.Info, (LogLevel level, string message) =>
            {
                Logging.Log($"Discord: {message}", (BepInEx.Logging.LogLevel)Enum.Parse(typeof(BepInEx.Logging.LogLevel), level.ToString().Replace(nameof(LogLevel.Warn), "Warning")));
            });

            activityManager = discord.GetActivityManager();

            if (activityManager == null)
            {
                Logging.Error("RegisterDiscord was interrupted as an ActivityManager is required for rich presence.");
                return;
            }

            activityManager.RegisterSteam(Constants.SteamID);

            activity.Timestamps.Start = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();

            activityManager.OnActivityJoin += (string secret) =>
            {
                Logging.Info($"OnActivityJoin {secret}");
                OnActivityJoin?.Invoke(secret);
            };

            activityManager.OnActivityJoinRequest += (ref User user) =>
            {
                Logging.Info($"OnActivityJoinRequest {user.Id}");

                Console.WriteLine("Join request from: {0}", user.Id);

                activityManager.SendRequestReply(user.Id, ActivityJoinRequestReply.Yes, (res) =>
                {
                    if (res == Result.Ok)
                    {
                        Console.WriteLine("Responded successfully");
                    }
                });
            };

            activityManager.OnActivityInvite += (ActivityActionType Type, ref User user, ref Activity activity2) =>
            {
                Logging.Info($"OnActivityInvite {Type} {JsonUtility.ToJson(user, true)} {activity2.Name}");

                Console.WriteLine("Received Invite Type: {0}, from User: {1}, with Activity Name: {2}", Type, user.Username, activity2.Name);

                activityManager.AcceptInvite(user.Id, result =>
                {
                    Console.WriteLine("AcceptInvite {0}", result);
                });
            };

            try
            {
                while (true)
                {
                    try
                    {
                        Thread.Sleep((int)(Constants.TickDebounce * 1000f));

                        if (toUpload && (Time.realtimeSinceStartup > (lastUploadTime + Constants.ActivityUploadDelay)))
                        {
                            toUpload = false;
                            lastUploadTime = Time.realtimeSinceStartup;

                            try
                            {
                                activityManager.UpdateActivity(activity, result => { });
                            }
                            catch (Exception ex)
                            {
                                Logging.Error(string.Concat("ActivityManager could not be updated: ", ex));
                            }
                        }

                        discord.RunCallbacks();
                    }
                    catch (ResultException ex)
                    {
                        Logging.Error(string.Concat("Discord threw a ResultException: ", ex.Message));
                    }
                }
            }
            finally
            {
                Logging.Info("Disposing of our Discord instance");

                discord.Dispose();
                discord = null;
            }
        }

        public static void SetActivity(Func<Activity, Activity> modificationFunc)
        {
            Activity modifiedActivity = modificationFunc(activity);
            activity = modifiedActivity;
        }

        public static void UpdateActivity(bool updateSecrets = true)
        {
            if (updateSecrets)
            {
                List<string> join_secrets = [];
                if (NetworkSystem.Instance.InRoom)
                {
                    join_secrets.Add(NetworkSystem.Instance.RoomName);
                    var authenticationValues = NetworkSystem.Instance.GetAuthenticationValues();
                    var dictionary = (authenticationValues?.AuthPostData) as Dictionary<string, object>;
                    if (dictionary != null && dictionary.TryGetValue("Zone", out object zone)) join_secrets.Add(zone.ToString());
                }

                string join_secret_string = join_secrets.Count > 0 ? string.Join("\n", join_secrets) : string.Empty;
                Logging.Info($"join secrets: {join_secret_string}");
                activity.Secrets.Join = join_secret_string;
                activity.Secrets.Match = string.IsNullOrEmpty(join_secret_string) ? string.Empty : "foo match";
                activity.Secrets.Spectate = string.IsNullOrEmpty(join_secret_string) ? string.Empty : "foo spectate";
            }

            toUpload = true;
        }
    }
}
