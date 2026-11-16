using System;
using System.Collections.Generic;
using System.Text;

namespace KioskStand
{
    /// <summary>
    /// Компактный генератор QR-кодов (байтовый режим, уровень коррекции M, версии 1–40).
    /// Алгоритм по стандарту ISO/IEC 18004 (по мотивам открытой реализации Project Nayuki, MIT).
    /// </summary>
    public class QrCode
    {
        public readonly int Size;
        readonly bool[,] modules;   // [y, x]
        readonly bool[,] isFunction;
        readonly int version;

        static readonly int[] EccPerBlockM = { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28 };
        static readonly int[] NumBlocksM = { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49 };
        const int EclFormatBitsM = 0;

        public bool Get(int x, int y) { return modules[y, x]; }

        public static QrCode Encode(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text ?? "");
            int ver;
            for (ver = 1; ; ver++)
            {
                if (ver > 40) throw new ArgumentException("Слишком длинная строка для QR-кода");
                int cc = ver <= 9 ? 8 : 16;
                int capBits = NumDataCodewords(ver) * 8;
                if (4 + cc + data.Length * 8 <= capBits) break;
            }
            var bits = new List<bool>();
            AppendBits(bits, 0x4, 4);
            AppendBits(bits, data.Length, ver <= 9 ? 8 : 16);
            foreach (byte b in data) AppendBits(bits, b, 8);
            int capacity = NumDataCodewords(ver) * 8;
            AppendBits(bits, 0, Math.Min(4, capacity - bits.Count));
            AppendBits(bits, 0, (8 - bits.Count % 8) % 8);
            for (int pad = 0xEC; bits.Count < capacity; pad ^= 0xEC ^ 0x11) AppendBits(bits, pad, 8);

            var codewords = new byte[bits.Count / 8];
            for (int i = 0; i < bits.Count; i++) if (bits[i]) codewords[i >> 3] |= (byte)(1 << (7 - (i & 7)));
            return new QrCode(ver, codewords);
        }

        static void AppendBits(List<bool> bb, int val, int len)
        {
            for (int i = len - 1; i >= 0; i--) bb.Add(((val >> i) & 1) != 0);
        }

        QrCode(int ver, byte[] dataCodewords)
        {
            version = ver;
            Size = ver * 4 + 17;
            modules = new bool[Size, Size];
            isFunction = new bool[Size, Size];
            DrawFunctionPatterns();
            byte[] all = AddEccAndInterleave(dataCodewords);
            DrawCodewords(all);

            int bestMask = 0; long minPenalty = long.MaxValue;
            for (int m = 0; m < 8; m++)
            {
                ApplyMask(m);
                DrawFormatBits(m);
                long p = Penalty();
                if (p < minPenalty) { bestMask = m; minPenalty = p; }
                ApplyMask(m); // XOR — отменяет маску
            }
            ApplyMask(bestMask);
            DrawFormatBits(bestMask);
        }

        void SetF(int x, int y, bool dark) { modules[y, x] = dark; isFunction[y, x] = true; }

        void DrawFunctionPatterns()
        {
            for (int i = 0; i < Size; i++) { SetF(6, i, i % 2 == 0); SetF(i, 6, i % 2 == 0); }
            DrawFinder(3, 3); DrawFinder(Size - 4, 3); DrawFinder(3, Size - 4);
            int[] pos = AlignmentPositions();
            int n = pos.Length;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == n - 1) || (i == n - 1 && j == 0)) continue;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                            SetF(pos[i] + dx, pos[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                }
            DrawFormatBits(0);
            DrawVersion();
        }

        void DrawFinder(int x, int y)
        {
            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && xx < Size && yy >= 0 && yy < Size) SetF(xx, yy, dist != 2 && dist != 4);
                }
        }

        int[] AlignmentPositions()
        {
            if (version == 1) return new int[0];
            int numAlign = version / 7 + 2;
            int step = (version * 8 + numAlign * 3 + 5) / (numAlign * 4 - 4) * 2;
            var result = new int[numAlign];
            result[0] = 6;
            for (int i = numAlign - 1, pos = Size - 7; i >= 1; i--, pos -= step) result[i] = pos;
            return result;
        }

        static bool Bit(int x, int i) { return ((x >> i) & 1) != 0; }

        void DrawFormatBits(int mask)
        {
            int data = EclFormatBitsM << 3 | mask;
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = (data << 10 | rem) ^ 0x5412;
            for (int i = 0; i <= 5; i++) SetF(8, i, Bit(bits, i));
            SetF(8, 7, Bit(bits, 6));
            SetF(8, 8, Bit(bits, 7));
            SetF(7, 8, Bit(bits, 8));
            for (int i = 9; i < 15; i++) SetF(14 - i, 8, Bit(bits, i));
            for (int i = 0; i < 8; i++) SetF(Size - 1 - i, 8, Bit(bits, i));
            for (int i = 8; i < 15; i++) SetF(8, Size - 15 + i, Bit(bits, i));
            SetF(8, Size - 8, true);
        }

        void DrawVersion()
        {
            if (version < 7) return;
            int rem = version;
            for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            int bits = version << 12 | rem;
            for (int i = 0; i < 18; i++)
            {
                bool b = Bit(bits, i);
                int a = Size - 11 + i % 3, c = i / 3;
                SetF(a, c, b); SetF(c, a, b);
            }
        }

        static int NumRawDataModules(int ver)
        {
            int result = (16 * ver + 128) * ver + 64;
            if (ver >= 2)
            {
                int numAlign = ver / 7 + 2;
                result -= (25 * numAlign - 10) * numAlign - 55;
                if (ver >= 7) result -= 36;
            }
            return result;
        }

        static int NumDataCodewords(int ver)
        {
            return NumRawDataModules(ver) / 8 - EccPerBlockM[ver] * NumBlocksM[ver];
        }

        byte[] AddEccAndInterleave(byte[] data)
        {
            int numBlocks = NumBlocksM[version];
            int blockEccLen = EccPerBlockM[version];
            int rawCodewords = NumRawDataModules(version) / 8;
            int numShortBlocks = numBlocks - rawCodewords % numBlocks;
            int shortBlockLen = rawCodewords / numBlocks;

            var blocks = new List<byte[]>();
            byte[] rsDiv = RsDivisor(blockEccLen);
            for (int i = 0, k = 0; i < numBlocks; i++)
            {
                int datLen = shortBlockLen - blockEccLen + (i < numShortBlocks ? 0 : 1);
                var dat = new byte[datLen];
                Array.Copy(data, k, dat, 0, datLen);
                k += datLen;
                byte[] ecc = RsRemainder(dat, rsDiv);
                var block = new byte[shortBlockLen + 1];
                Array.Copy(dat, 0, block, 0, datLen);
                // у коротких блоков после данных — пустая ячейка-заполнитель
                Array.Copy(ecc, 0, block, block.Length - blockEccLen, blockEccLen);
                blocks.Add(block);
            }
            var result = new List<byte>();
            for (int i = 0; i < blocks[0].Length; i++)
                for (int j = 0; j < blocks.Count; j++)
                    if (i != shortBlockLen - blockEccLen || j >= numShortBlocks) result.Add(blocks[j][i]);
            return result.ToArray();
        }

        static byte[] RsDivisor(int degree)
        {
            var result = new byte[degree];
            result[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = (byte)RsMul(result[j], root);
                    if (j + 1 < degree) result[j] ^= result[j + 1];
                }
                root = RsMul(root, 0x02);
            }
            return result;
        }

        static byte[] RsRemainder(byte[] data, byte[] divisor)
        {
            var result = new byte[divisor.Length];
            foreach (byte b in data)
            {
                int factor = b ^ result[0];
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int i = 0; i < result.Length; i++) result[i] ^= (byte)RsMul(divisor[i], factor);
            }
            return result;
        }

        static int RsMul(int x, int y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return z & 0xFF;
        }

        void DrawCodewords(byte[] data)
        {
            int i = 0;
            for (int right = Size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < Size; vert++)
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? Size - 1 - vert : vert;
                        if (!isFunction[y, x] && i < data.Length * 8)
                        {
                            modules[y, x] = Bit(data[i >> 3], 7 - (i & 7));
                            i++;
                        }
                    }
            }
        }

        void ApplyMask(int mask)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    bool inv;
                    switch (mask)
                    {
                        case 0: inv = (x + y) % 2 == 0; break;
                        case 1: inv = y % 2 == 0; break;
                        case 2: inv = x % 3 == 0; break;
                        case 3: inv = (x + y) % 3 == 0; break;
                        case 4: inv = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: inv = x * y % 2 + x * y % 3 == 0; break;
                        case 6: inv = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        default: inv = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                    }
                    if (inv && !isFunction[y, x]) modules[y, x] = !modules[y, x];
                }
        }

        // Упрощённая оценка штрафа по правилам стандарта (для выбора маски).
        long Penalty()
        {
            long p = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int a = 0; a < Size; a++)
                {
                    int run = 1;
                    for (int b = 1; b < Size; b++)
                    {
                        bool cur = pass == 0 ? modules[a, b] : modules[b, a];
                        bool prev = pass == 0 ? modules[a, b - 1] : modules[b - 1, a];
                        if (cur == prev) { run++; if (run == 5) p += 3; else if (run > 5) p++; }
                        else run = 1;
                    }
                    // шаблон 1:1:3:1:1 с белыми полями
                    for (int b = 0; b + 10 < Size; b++)
                    {
                        bool[] w = new bool[11];
                        for (int k = 0; k < 11; k++) w[k] = pass == 0 ? modules[a, b + k] : modules[b + k, a];
                        bool p1 = w[0] && !w[1] && w[2] && w[3] && w[4] && !w[5] && w[6] && !w[7] && !w[8] && !w[9] && !w[10];
                        bool p2 = !w[0] && !w[1] && !w[2] && !w[3] && w[4] && !w[5] && w[6] && w[7] && w[8] && !w[9] && w[10];
                        if (p1 || p2) p += 40;
                    }
                }
            for (int y = 0; y < Size - 1; y++)
                for (int x = 0; x < Size - 1; x++)
                {
                    bool c = modules[y, x];
                    if (c == modules[y, x + 1] && c == modules[y + 1, x] && c == modules[y + 1, x + 1]) p += 3;
                }
            int dark = 0;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) if (modules[y, x]) dark++;
            int total = Size * Size;
            int kk = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            p += Math.Max(0, kk) * 10;
            return p;
        }
    }
}
