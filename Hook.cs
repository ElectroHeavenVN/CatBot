using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using HarmonyLib;
using TagLib;

namespace CatBot
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
                if (__instance.TargetChannel.Type == ChannelType.Stage)
                    speaking = true;
                return true;
            }
        }

        [HarmonyPatch(typeof(Picture), nameof(Picture.GetExtensionFromMime))]
        internal class GetExtensionFromMimeHook
        {
            static bool Prefix(string mime, ref string __result)
            {
                if (mime == "image/jpg")
                {
                    __result = "jpg";
                    return false;
                }
                return true;
            }
        }
    }
}
