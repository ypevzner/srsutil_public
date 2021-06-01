using System;
using System.Collections.Generic;

namespace FDA.SRS.Utils
{
	public static class ForEachExtensions
	{
        public static HashSet<T> ToHashSet<T>(
        this IEnumerable<T> source,
        IEqualityComparer<T> comparer = null) {
            return new HashSet<T>(source, comparer);
        }

        public static String JoinToString<T>(
        this IEnumerable<T> source,
        String delim) {
            return String.Join(delim,source);
        }

        public static void ForEachWithIndex<T>(this IEnumerable<T> enumerable, Action<T, int> handler)
		{
			int idx = 0;
			foreach ( T item in enumerable )
				handler(item, idx++);
		}
        public static IEnumerable<U> Map<T,U>(this IEnumerable<T> enumerable, Func<T, U> map){
            List<U> list = new List<U>();

            enumerable.ForEachWithIndex((t, h) =>{
                list.Add(map.Invoke(t));
            });
            return list;
        }
        public static IEnumerable<T> Filter<T>(this IEnumerable<T> enumerable, Predicate<T> pred)
        {
            List<T> list = new List<T>();

            enumerable.ForEachWithIndex((t, h) => {
                if (pred.Invoke(t)){
                    list.Add(t);
                }
            });
            return list;
        }
        /*
        public static IEnumerable<T> ToList<T>(this IEnumerable<T> enumerable){
            if (enumerable is List<T>) return (List<T>)enumerable;
            List<T> list = new List<T>();
            enumerable.ForEachWithIndex((t, h) => {
                list.Add(t);
            });
            return list;
        }
        */
    }
}
