using System;
using System.Diagnostics;
using System.Reflection;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace DiscordBot
{
    internal class Hook
    {
        [HarmonyPatch("DSharpPlus.VoiceNext.Entities.VoiceStateUpdatePayload", "Deafened", MethodType.Setter)]
        internal class DeafenHook
        {
            internal static bool isDeafen = true;
            static bool Prefix(ref bool value)
            {
                value = isDeafen;
                return true;
            }
        }

        [HarmonyPatch("DSharpPlus.Utilities", "IsTextableChannel")]
        internal class IsTextableChannelHook
        {
            static bool Prefix(DiscordChannel channel, ref bool __result)
            {
                if (channel.Type == ChannelType.Stage)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch("DSharpPlus.VoiceNext.VoiceNextConnection", "SendSpeakingAsync")]
        internal class SendSpeakingHook
        {
            static bool Prefix(VoiceNextConnection __instance, ref bool speaking)
            {
                if (__instance.TargetChannel.Type == DSharpPlus.ChannelType.Stage)
                    speaking = true;
                return true;
            }
        }

        //[HarmonyPatch(typeof(JObject), "get_Item")]
        //[HarmonyPatch(typeof(JObject), new Type[] { typeof(string) })]
        internal class GetItemHook
        {
            //fck VPS
            static void Postfix(ref JToken __result, string propertyName)
            {
                MethodBase methodBase = new StackFrame(2).GetMethod();
                if (propertyName == "op" && methodBase.Name == "MoveNext" && methodBase.DeclaringType.Name == "<HandleDispatch>d__222" && (int)__result == 18)
                {
                    __result = 12;
                }
            }
        }
    }
}
