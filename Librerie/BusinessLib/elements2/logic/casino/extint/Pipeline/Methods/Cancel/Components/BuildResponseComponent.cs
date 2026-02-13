namespace GamingTests.Librerie.BusinessLib.elements2.logic.casino.extint.Pipeline.Methods.Cancel.Components
{
    /// <summary>
    /// Componente standard: Build Response.
    /// Costruisce la response finale basandosi sullo stato del contesto.
    /// </summary>
    public static class BuildResponseComponent
    {
        public const string Key = "BuildResponse";

        public static void Execute(CancelContext ctx)
        {
            ctx.Response["responseCodeReason"] = ctx.TargetStatus;

            if (ctx.TargetStatus == "200")
            {
                ctx.Response.Remove("errorMessage");
            }
            else
            {
                if (!ctx.Response.ContainsKey("errorMessage"))
                    ctx.Response["errorMessage"] = "ERROR";
            }

            if (ctx.NewMov != null)
                ctx.Response["casinoTransferId"] = ctx.NewMov.CMB_ID.ToString();
        }
    }
}
