using System.Collections.Generic;

namespace CatBot.Music.NhacCuaTui
{ 
    internal class ARC4
    {
        int _i = 0;
        int _j = 0;
        List<int> _state = new List<int>(256);

        internal void LoadKey(List<int> key)
        {
            _state = new List<int>(256);
            if (key is not null && key.Count > 0)
                Initialize(key);
        }

        internal void Initialize(List<int> key)
        {
            for (int k = 0; k < 256; ++k)
                _state.Add(k);
            int j = 0;
            for (int i = 0; i < 256; ++i)
            {
                j = (j + _state[i] + key[i % key.Count]) & 255;
                (_state[j], _state[i]) = (_state[i], _state[j]);
            }
            _i = 0;
            _j = 0;
        }

        internal int NextByte()
        {
            _i = (_i + 1) & 255;
            _j = (_j + _state[_i]) & 255;
            (_state[_j], _state[_i]) = (_state[_i], _state[_j]);
            return _state[(_state[_i] + _state[_i]) & 255];
        }

        internal List<int> EncryptBlock(List<int> block)
        {
            for (int k = 0; k < block.Count; k++)
                block[k] ^= NextByte();
            return block;
        }

        internal List<int> DecryptBlock(List<int> block) => EncryptBlock(block); // the beauty of XOR.
    }
}
