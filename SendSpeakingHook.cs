using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.VoiceNext;
using HarmonyLib;

namespace DiscordBot
{
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
}
