using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace VRGIN.Helpers
{
    public class JSONArray : JSONNode, IEnumerable
    {
        private List<JSONNode> m_List = new List<JSONNode>();

        public override JSONNode this[int aIndex]
        {
            get
            {
                if (aIndex < 0 || aIndex >= m_List.Count) return new JSONLazyCreator(this);
                return m_List[aIndex];
            }
            set
            {
                if (aIndex < 0 || aIndex >= m_List.Count)
                    m_List.Add(value);
                else
                    m_List[aIndex] = value;
            }
        }

        public override JSONNode this[string aKey]
        {
            get => new JSONLazyCreator(this);
            set => m_List.Add(value);
        }

        public override int Count => m_List.Count;

        public override IEnumerable<JSONNode> Children
        {
            get
            {
                foreach (var item in m_List) yield return item;
            }
        }

        public override void Add(string aKey, JSONNode aItem)
        {
            m_List.Add(aItem);
        }

        public override JSONNode Remove(int aIndex)
        {
            if (aIndex < 0 || aIndex >= m_List.Count) return null;
            var result = m_List[aIndex];
            m_List.RemoveAt(aIndex);
            return result;
        }

        public override JSONNode Remove(JSONNode aNode)
        {
            m_List.Remove(aNode);
            return aNode;
        }

        public IEnumerator GetEnumerator()
        {
            foreach (var item in m_List) yield return item;
        }

        public override string ToString()
        {
            var text = "[ ";
            foreach (var item in m_List)
            {
                if (text.Length > 2) text += ", ";
                text += item.ToString();
            }

            return text + " ]";
        }

        public override string ToString(string aPrefix)
        {
            var text = "[ ";
            foreach (var item in m_List)
            {
                if (text.Length > 3) text += ", ";
                text = text + "\n" + aPrefix + "   ";
                text += item.ToString(aPrefix + "   ");
            }

            return text + "\n" + aPrefix + "]";
        }

        public override string ToJSON(int prefix)
        {
            var text = new string(' ', (prefix + 1) * 2);
            var text2 = "[ ";
            foreach (var item in m_List)
            {
                if (text2.Length > 3) text2 += ", ";
                text2 = text2 + "\n" + text;
                text2 += item.ToJSON(prefix + 1);
            }

            return text2 + "\n" + text + "]";
        }

        public override void Serialize(BinaryWriter aWriter)
        {
            aWriter.Write((byte)1);
            aWriter.Write(m_List.Count);
            for (var i = 0; i < m_List.Count; i++) m_List[i].Serialize(aWriter);
        }
    }
}
