using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatBot.Music.NhacCuaTui
{ 
    internal class ARC4
    {
        int i = 0;
        int j = 0;
        List<int> S = new List<int>(256);

        internal void Load(List<int> key)
        {
            S = new List<int>(256);
            if (key != null && key.Count > 0)
                Init(key);
        }

        internal void Init(List<int> key)
        {
            for (int i = 0; i < 256; ++i)
                S.Add(i);
            int j = 0;
            for (i = 0; i < 256; ++i)
            {
                j = (j + S[i] + key[i % key.Count]) & 255;
                (S[j], S[i]) = (S[i], S[j]);
            }
            i = 0;
            this.j = 0;
        }

        internal int Next()
        {
            i = (i + 1) & 255;
            j = (j + S[i]) & 255;
            (S[j], S[i]) = (S[i], S[j]);
            return S[(S[i] + S[i]) & 255];
        }

        internal List<int> Encrypt(List<int> block)
        {
            for (int i = 0; i < block.Count; i++)
                block[i] ^= Next();
            return block;
        }

        internal List<int> Decrypt(List<int> block) => Encrypt(block); // the beauty of XOR.
    }
}
