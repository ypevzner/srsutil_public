using System.Collections.Generic;

namespace FDA.SRS.Utils
{
	public class Permutation
    {
        private List<int> list;
        public Permutation(List<int> l, bool restart=true)
        {
            list = l;
            if (restart) list.Sort();
        }
        private Permutation(Permutation p)
        {
            this.list = p.list;
        }
        public List<int> Config
        {
            get { return list; }
        }
        public Permutation NextPermutation()
        {
            Permutation next = new Permutation(this);
            int left = next.list.Count - 2;
            int right;
            if (left < 0) return null;
            while (next.list[left] >= next.list[left + 1] && left >= 1)
            {
                --left;
            }
            if (left == 0 && this.list[left] >= this.list[left + 1])
            {
                return null;
            }
            right = next.list.Count - 1;
            while (next.list[left] >= next.list[right])
            {
                --right;
            }

            int temp = next.list[left];
            next.list[left] = next.list[right];
            next.list[right] = temp;
            int i = left + 1;
            int j = next.list.Count - 1;
            while (i < j)
            {
                temp = next.list[i];
                next.list[i++] = next.list[j];
                next.list[j--] = temp;
            }

            return next;
        }
    }
}


