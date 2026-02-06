using System;
using it.capecod.config;
using it.capecod.data;
using it.capecod.gridgame.business.elements2.logic.account.ext;
using it.capecod.gridgame.business.elements2.logic.casino.extint;
using it.capecod.gridgame.business.elements2.logic.casino.extint.am.types;
using it.capecod.inject;
using it.capecod.util;

namespace GamingTest.BusinessLib.elements2.logic.casino.extint.am
{
    public class CasinoAMSwDispatchTest : FinMovExtWsFace
    {
        public static string _AMSWFACEKEY = CcConfig.AppSettingStr("CASINOAMSW_FACE_KEY", "AMSW", "EXTAMFACEKEY");

        public static string AMSWFACEKEY
        {
            get
            {
                if(!CasinoExtIntMgr.def.checkSignedFace(_AMSWFACEKEY))
                    CasinoExtIntMgr.def.registerSignedFace(_AMSWFACEKEY, CasinoExtIntAMSWCoreTest.def);
                return _AMSWFACEKEY;
            }
        }

        public static CasinoAMSwDispatchTest _def;
        public static bool defReload;

        public static CasinoAMSwDispatchTest def
        {
            get
            {
                if (_def == null || defReload)
                {
                    _def = (CasinoAMSwDispatchTest)CCFactory.Get(typeof(CasinoAMSwDispatchTest));
                    defReload = false;
                }
                return _def;
            }
        }

        public override DTH Special(int eiId, int euId, string idTransazione, string author, HashParams auxPars)
        {
            auxPars["author"] = author;

            HashResult hashResult = CasinoExtIntAMSWCore.def.
                getAuxInfos(
                    auxPars.getTypedValue("method", string.Empty, false),
                    new HashParams(
                        "euId", euId,
                        "callPars", auxPars
                        )
                );
            //evnt per performance si può fare serializz manuale
            if (hashResult.IsOk)
            {
                DTH result = DTOMgr.def.DTOtoDTH((DTOFace)hashResult["MSGRESULT"], string.Empty, new HashParams("extendToFields", true));
                result["_targetStatus"] = (int)auxPars.getTypedValue("_targetStatus", Cam_ResponseStatus.RS_200_Success);
                result["_isRetransmission"] = auxPars.getTypedValue("_isRetransmission", false);
                string[] mirrorKeys = new[] { "_cmbId", "_partition", "_matchId", "_opDateUtc" };
                foreach (var key in mirrorKeys)
                {
                    if (auxPars.ContainsKey(key))
                        result[key] = auxPars[key];
                }
                return result;
            }
            throw new Exception(hashResult.ErrorMessage); //in teoria mai
        }

        public override DTH GenericTransaction(int eiId, int txType, int euId, string idTransazione, string author, HashParams auxPars)
        {
            throw new Exception(GetType().Name + ": TransactionType not implemented");
        }

    }
}

