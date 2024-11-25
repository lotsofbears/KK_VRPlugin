using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace Valve.VR
{
    [StructLayout(LayoutKind.Sequential, Size = 1)]
    internal struct SteamVREnumEqualityComparer<TEnum> : IEqualityComparer<TEnum> where TEnum : struct
    {
        private static class BoxAvoidance
        {
            private static readonly Func<TEnum, int> _wrapper;

            public static int ToInt(TEnum enu)
            {
                return _wrapper(enu);
            }

            static BoxAvoidance()
            {
                var parameterExpression = Expression.Parameter(typeof(TEnum), null);
                _wrapper = Expression.Lambda<Func<TEnum, int>>(Expression.ConvertChecked(parameterExpression, typeof(int)), new ParameterExpression[1] { parameterExpression }).Compile();
            }
        }

        public bool Equals(TEnum firstEnum, TEnum secondEnum)
        {
            return BoxAvoidance.ToInt(firstEnum) == BoxAvoidance.ToInt(secondEnum);
        }

        public int GetHashCode(TEnum firstEnum)
        {
            return BoxAvoidance.ToInt(firstEnum);
        }
    }
}
