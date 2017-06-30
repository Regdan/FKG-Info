﻿namespace FKG_Info
{
    public class SkillInfo
    {
        public int ID { get; private set; }

        public string KName;
        public string KInfo;

        private int SkillType;

        private int[] Params;

        public int ChanceMin { get; private set; }
        public int ChanceMax { get; private set; }



        public SkillInfo()
        {
            Params = new int[3];
        }



        public SkillInfo(string[] masterData) : this()
        {
            if (masterData.Length < 14) return;

            int parseValue;
            if (!int.TryParse(masterData[0], out parseValue)) { ID = 0; return; }
            ID = parseValue;

            KName = masterData[1];
            KInfo = masterData[6];

            int.TryParse(masterData[2], out SkillType);

            for (int i = 0; i < Params.Length; i++) int.TryParse(masterData[i + 3], out Params[i]);

            int.TryParse(masterData[7], out parseValue);
            ChanceMin = parseValue;
            int.TryParse(masterData[8], out parseValue);
            ChanceMax = parseValue;

            ChanceMax = ChanceMin + 5 * ChanceMax;
        }


        /// <summary>
        /// Return (string) skill description.
        /// </summary>
        /// <param name="translation"></param>
        /// <param name="mode">0=Full, 1=Name, 2=Chance, 3=Full Desc. By default Full.</param>
        /// <returns></returns>
        public string GetInfo(int mode = 0, bool translation = true)
        {
            string info = "";

            switch (mode)
            {
                case 1: return KName;
                case 2: return ChanceMin + " .. " + ChanceMax + "%";
                case 3:
                    info = KInfo;
                    if (translation)
                    {
                        string tr = Program.DB.GetSkillTranslation(SkillType);
                        if (tr != null) info = string.Format(tr, Params[0], Params[1], Params[2]);
                        info = StringHelper.ReplaceSimpleArithmetic(info, '+');
                    }
                    return info;
                default:
                    info = "Name: " + GetInfo(1) + "\r\nChance: " + GetInfo(2) + "\r\n" + GetInfo(3, translation);
                    break;
            }

            return info;
        }
    }
}
