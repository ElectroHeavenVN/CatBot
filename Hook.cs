using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using HarmonyLib;
using TagLib;

namespace CatBot
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    internal class Hook
    {
        [HarmonyPatch("DSharpPlus.VoiceNext.Entities.VoiceStateUpdatePayload", "Deafened", MethodType.Setter)]
        internal class DeafenHook
        {
            static readonly bool isDeafen = true;
            static bool Prefix(ref bool value)
            {
                value = isDeafen;
                return true;
            }
        }

        [HarmonyPatch(typeof(VoiceNextConnection), nameof(VoiceNextConnection.SendSpeakingAsync))]
        internal class SendSpeakingHook
        {
            static bool Prefix(VoiceNextConnection __instance, ref bool speaking)
            {
                if (__instance.TargetChannel.Type == DiscordChannelType.Stage)
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

        //
        //[HarmonyPatch(typeof(CommandsNextExtension), "HandleCommandsAsync")]
        //internal class HandleCommandAsyncHook
        //{
        //    static bool CheckExcludedBot(DiscordUser user) => user.IsBot && !user.IsBotExcluded();

        //    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        //    {
        //        foreach (var instruction in instructions)
        //        {
        //            if (instruction.opcode == OpCodes.Call)
        //            {
        //                if (instruction.operand is MethodInfo methodInfo && methodInfo.Name == "get_IsBot")
        //                {
        //                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HandleCommandAsyncHook), nameof(CheckExcludedBot)));
        //                    continue;
        //                }
        //            }
        //            yield return instruction;
        //        }
        //    }
        //}
    }
}
