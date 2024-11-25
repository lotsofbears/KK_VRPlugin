using System;
using System.Collections.Generic;

namespace Valve.VR
{
    [Serializable]
    public class SteamVR_Input_BindingFile_Source_Input : Dictionary<string, SteamVR_Input_BindingFile_Source_Input_StringDictionary>
    {
        public override bool Equals(object obj)
        {
            if (obj is SteamVR_Input_BindingFile_Source_Input)
            {
                var steamVR_Input_BindingFile_Source_Input = (SteamVR_Input_BindingFile_Source_Input)obj;
                if (this == steamVR_Input_BindingFile_Source_Input) return true;
                if (Count == steamVR_Input_BindingFile_Source_Input.Count)
                {
                    using (var enumerator = GetEnumerator())
                    {
                        while (enumerator.MoveNext())
                        {
                            var current = enumerator.Current;
                            if (!steamVR_Input_BindingFile_Source_Input.ContainsKey(current.Key)) return false;
                            if (!base[current.Key].Equals(steamVR_Input_BindingFile_Source_Input[current.Key])) return false;
                        }
                    }

                    return true;
                }
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
