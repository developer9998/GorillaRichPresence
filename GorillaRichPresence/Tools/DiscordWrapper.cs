﻿using Discord;
using GorillaInfoWatch;
using GorillaInfoWatch.Models;
using GorillaNetworking;
using GorillaRichPresence.Models;
using GorillaRichPresence.Utils;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

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

            discord.SetLogHook(LogLevel.Info, (level, message) =>
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

            // user joins you
            activityManager.OnActivityJoin += (string secret) =>
            {
                Logging.Info($"OnActivityJoin {secret}");
                OnActivityJoin?.Invoke(secret);
            };

            // user asks whether they can join you
            activityManager.OnActivityJoinRequest += (ref User user) =>
            {
                User requestingUser = user;

                Logging.Info($"OnActivityJoinRequest {JsonUtility.ToJson(user, true)}");

                string avatarUrl = string.Format("https://cdn.discordapp.com/avatars/{0}/{1}.png?size=128", user.Id, user.Avatar);
                UnityWebRequest request = UnityWebRequestTexture.GetTexture(avatarUrl);
                TaskUtils.Yield(request).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Logging.Error(task.Exception);
                        return;
                    }

                    Logging.Info($"Task completed");

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Texture tex = ((DownloadHandlerTexture)request.downloadHandler).texture;
                        Logging.Info("Got avatar");

                        void ReplyChosen(User user, ActivityJoinRequestReply reply)
                        {
                            if (user.Id == requestingUser.Id)
                            {
                                JoinRequestScreen.sendReply -= ReplyChosen;
                                activityManager.SendRequestReply(user.Id, reply, (res) =>
                                {
                                    if (res == Result.Ok)
                                    {
                                        Logging.Info("Responded successfully");
                                    }
                                });
                            }
                        }

                        Events.SendNotification(new("You received a join request", requestingUser.Username, 5, InfoWatchSound.notificationPositive, new(typeof(JoinRequestScreen), "Invite", delegate ()
                        {
                            JoinRequestScreen.hasUser = true;
                            JoinRequestScreen.requestingUser = requestingUser;
                            JoinRequestScreen.requestingAvatar = tex;
                            JoinRequestScreen.sendReply += ReplyChosen;
                        })));
                    }
                });
            };

            // invited to play by another user
            activityManager.OnActivityInvite += (ActivityActionType Type, ref User user, ref Activity activity2) =>
            {
                User requestingUser = user;

                Logging.Info($"OnActivityInvite {Type} {JsonUtility.ToJson(user, true)} {activity2.Name}");

                string avatarUrl = string.Format("https://cdn.discordapp.com/avatars/{0}/{1}.png?size=128", user.Id, user.Avatar);
                UnityWebRequest request = UnityWebRequestTexture.GetTexture(avatarUrl);
                TaskUtils.Yield(request).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Logging.Error(task.Exception);
                        return;
                    }

                    Logging.Info("Task completed");

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Texture tex = ((DownloadHandlerTexture)request.downloadHandler).texture;
                        Logging.Info("Got avatar");

                        void ReplyChosen(User user, bool accept)
                        {
                            if (user.Id == requestingUser.Id)
                            {
                                InviteRequestScreen.sendReply -= ReplyChosen;
                                if (accept)
                                {
                                    activityManager.AcceptInvite(user.Id, result =>
                                    {
                                        Logging.Info($"AcceptInvite {result}");
                                    });
                                }
                            }
                        }

                        Events.SendNotification(new("You received an invite", requestingUser.Username, 5, InfoWatchSound.notificationPositive, new(typeof(InviteRequestScreen), "Invite", delegate ()
                        {
                            InviteRequestScreen.hasUser = true;
                            InviteRequestScreen.requestingUser = requestingUser;
                            InviteRequestScreen.requestingAvatar = tex;
                            InviteRequestScreen.sendReply += ReplyChosen;
                        })));
                    }
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
                string joinSecret = "";

                if (NetworkSystem.Instance.InRoom)
                {
                    List<string> list = new();

                    list.Add(NetworkSystem.Instance.RoomName);

                    if (NetworkSystem.Instance.SessionIsPrivate)
                        list.Add(PhotonNetworkController.Instance.privateTrigger.networkZone);
                    else
                    {
                        AuthenticationValues authenticationValues = NetworkSystem.Instance.GetAuthenticationValues();
                        if (authenticationValues is not null && authenticationValues.AuthPostData is Dictionary<string, object> dictionary && dictionary.TryGetValue("Zone", out object zone))
                            list.Add(zone.ToString());
                        else
                            list.Add("none");
                    }

                    joinSecret = string.Join("\n", list);
                    Logging.Info(joinSecret);
                }

                if (activity.Secrets.Join != joinSecret)
                {
                    Logging.Info($"Secret: {string.Join(", ", joinSecret.Split("\n"))}");
                    activity.Secrets.Join = joinSecret;
                }

                activity.Secrets.Match = string.IsNullOrEmpty(joinSecret) ? "" : "foo match";
                activity.Secrets.Spectate = string.IsNullOrEmpty(joinSecret) ? "" : "foo spectate";
            }

            toUpload = true;
        }
    }
}
