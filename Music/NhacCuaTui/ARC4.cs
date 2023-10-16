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
        int psize = 256;

        internal void Load(List<int> key)
        {
            S = new List<int>(256);
            if (key != null && key.Count > 0)
                Init(key);
        }

        internal void Init(List<int> key)
        {
            var i = 0;
            var j = 0;
            var t = 0;
            for (i = 0; i < 256; ++i)
            {
                S.Add(i);
            }
            j = 0;
            for (i = 0; i < 256; ++i)
            {
                j = (j + S[i] + key[i % key.Count]) & 255;
                t = S[i];
                S[i] = S[j];
                S[j] = t;
            }
            this.i = 0;
            this.j = 0;
        }

        internal int Next()
        {
            var t = 0;
            i = (i + 1) & 255;
            j = (j + S[i]) & 255;
            t = S[i];
            S[i] = S[j];
            S[j] = t;
            return S[(t + S[i]) & 255];
        }

        internal int GetBlockSize()
        {
            return 1;
        }

        internal List<int> Encrypt(List<int> block)
        {
            var i = 0;
            while (i < block.Count)
            {
                block[i++] ^= Next();
            }
            return block;
        }

        internal List<int> Decrypt(List<int> block)
        {
            return Encrypt(block); // the beauty of XOR.
        }

        internal void Dispose() //no idea why?
        {
            Random random = new Random();
            var i = 0;
            if (S != null)
            {
                for (i = 0; i < S.Count; i++)
                {
                    S[i] = (int)(random.NextDouble() * 256);
                }
                S = null;
            }
            this.i = 0;
            j = 0;
        }
    }
}
