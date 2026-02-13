using it.capecod.log;
using Newtonsoft.Json;
using System;
using System.Collections;

namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Cancel.Components
{
    /// <summary>
    /// Componente standard: Resend.
    /// Gestisce la risposta per transazioni idempotenti (già processate).
    /// </summary>
    public static class ResendComponent
    {
        public const string Key = "Resend";

        public static void Execute(CancelContext ctx)
        {
            var op = ctx.IdempotencyMov;
            if (op == null)
                return;

            try
            {
                if (!string.IsNullOrWhiteSpace(op.CMB_SENTTEXT))
                {
                    var ht = JsonConvert.DeserializeObject<Hashtable>(op.CMB_SENTTEXT);
                    if (ht != null)
                    {
                        ctx.Response.Clear();
                        foreach (DictionaryEntry e in ht)
                            ctx.Response[e.Key] = e.Value;
                    }
                }

                var status = op.CMB_RESULT;
                if (string.IsNullOrWhiteSpace(status))
                    status = "200";

                ctx.TargetStatus = status;
                ctx.Response["responseCodeReason"] = status;
                ctx.Response["casinoTransferId"] = op.CMB_ID.ToString();
            }
            catch (Exception ex)
            {
                Log.exc(ex);
                ctx.TargetStatus = "200";
                ctx.Response["responseCodeReason"] = "200";
                ctx.Response["casinoTransferId"] = op.CMB_ID.ToString();
            }
            finally
            {
                ctx.Stop = true;
            }
        }
    }
}
