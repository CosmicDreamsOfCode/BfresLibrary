﻿using System;
using System.Numerics;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Syroot.NintenTools.Bfres.Core;

namespace Syroot.NintenTools.Bfres.Switch
{
    public class ResDictUpdate
    {
        // ---- METHODS (PRIVATE) --------------------------------------------------------------------------------------

        static string ToBinaryString(string text, Encoding encoding)
        {
            return string.Join("", encoding.GetBytes(text).Select(n => Convert.ToString(n, 2).PadLeft(8, '0')));
        }

        static int _bit(BigInteger n, int b)
        {
            BigInteger test = (n >> (int)(b & 0xffffffff)) & 1;
            return (int)test;
        }

        static int first_1bit(BigInteger n)
        {
            int bitlength1 = BitLength(n);
            for (int i = 0; i < bitlength1; i++)
            {
                if (((n >> i) & 1) == 1)
                {
                    return i;
                }
            }

            throw new Exception("Operation Failed");
        }

        static int bit_mismatch(BigInteger int1, BigInteger int2)
        {
            int bitlength1 = BitLength(int1);
            int bitlength2 = BitLength(int2);

            for (int i = 0; i < Math.Max(bitlength1, bitlength2); i++)
            {
                if (((int1 >> i) & 1) != ((int2 >> i) & 1))
                    return i;
            }
            return -1;
        }


        static int BitLength(BigInteger bits)
        {
            int bitLength = 0;
            while (bits / 2 != 0)
            {
                bits /= 2;
                bitLength++;
            }
            bitLength += 1;
            return bitLength;
        }

        class Tree
        {
            public Node root;

            public Dictionary<BigInteger, Tuple<int, Node>> entries;

            public Tree()
            {
                entries = new Dictionary<BigInteger, Tuple<int, Node>>();

                root = new Node(0, -1, root);
                root.Parent = root;

                insertEntry(0, root);
            }

            int GetCompactBitIdx()
            {
                return -1;
            }

            public void insertEntry(BigInteger data, Node node)
            {
                entries[data] = (Tuple.Create(entries.Count, node));
            }

            Node Search(BigInteger data, bool prev)
            {
                if (root.Child[0] == root)
                    return root;

                Node node = root.Child[0];
                Node prevNode = node;
                while (true)
                {
                    prevNode = node;
                    node = node.Child[_bit(data, node.bitInx)];
                    if (node.bitInx <= prevNode.bitInx)
                        break;
                }
                if (prev)
                    return prevNode;
                else
                    return node;
            }

            public void Insert(string Name)
            {
                string bits = ToBinaryString(Name, Encoding.UTF8);
                BigInteger data = bits.Aggregate(new BigInteger(), (b, c) => b * 2 + c - '0');
                Node current = Search(data, true);
                int bitIdx = bit_mismatch(current.Data, data);

                while (bitIdx < current.Parent.bitInx)
                    current = current.Parent;

                if (bitIdx < current.bitInx)
                {
                    Node newNode = new Node(data, bitIdx, current.Parent);
                    newNode.Child[_bit(data, bitIdx) ^ 1] = current;
                    current.Parent.Child[_bit(data, current.Parent.bitInx)] = newNode;
                    current.Parent = newNode;

                    insertEntry(data, newNode);
                }
                else if (bitIdx > current.bitInx)
                {
                    Node newNode = new Node(data, bitIdx, current);
                    if (_bit(current.Data, bitIdx) == (_bit(data, bitIdx) ^ 1))
                        newNode.Child[_bit(data, bitIdx) ^ 1] = current;
                    else
                        newNode.Child[_bit(data, bitIdx) ^ 1] = root;


                    current.Child[_bit(data, current.bitInx)] = newNode;
                    insertEntry(data, newNode);
                }
                else
                {

                    int NewBitIdx = first_1bit(data);

                    if (current.Child[_bit(data, bitIdx)] != root)
                        NewBitIdx = bit_mismatch(current.Child[_bit(data, bitIdx)].Data, data);
                    Node newNode = new Node(data, NewBitIdx, current);

                    newNode.Child[_bit(data, NewBitIdx) ^ 1] = current.Child[_bit(data, bitIdx)];
                    current.Child[_bit(data, bitIdx)] = newNode;
                    insertEntry(data, newNode);
                }
            }
        }


        static internal Node[] UpdateNodes(List<string> keys)
        {
            Node[] _nodes = new Node[keys.Count+1];
            for (int i = 0; i < keys.Count; i++) {
                _nodes[i+1] = new Node();
                _nodes[i+1].Key = keys[i];
            }

            Tree tree = new Tree();

            // Create a new root node with empty key so the length can be retrieved throughout the process.
            _nodes[0] = new Node() { Key = String.Empty, bitInx = -1, Parent = _nodes[0] };

            // Update the data-referencing nodes.
            for (ushort i = 1; i < _nodes.Length; i++)
                tree.Insert(_nodes[i].Key);

            int CurEntry = 0;
            foreach (var entry in tree.entries.Values)
            {
                Node node = entry.Item2;

                node.Reference = (uint)(node.GetCompactBitIdx() & 0xffffffff);
                node.IdxLeft = (ushort)tree.entries[node.Child[0].Data].Item1;
                node.IdxRight = (ushort)tree.entries[node.Child[1].Data].Item1;
                node.Key = node.GetName();
                _nodes[CurEntry] = node;

                CurEntry++;
            }

            // Remove the dummy empty key in the root again.
            _nodes[0].Key = null;

            return _nodes;
        }

        private Node ReadNode(ResFileLoader loader)
        {
            return new Node()
            {
                Reference = loader.ReadUInt32(),
                IdxLeft = loader.ReadUInt16(),
                IdxRight = loader.ReadUInt16(),
                Key = loader.LoadString(),
            };
        }

        // ---- CLASSES ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Represents a node forming the Patricia trie of the dictionary.
        /// </summary>
        [DebuggerDisplay(nameof(Node) + " {" + nameof(Key) + "}")]
        internal class Node
        {
            internal const int SizeInBytes = 16;

            internal List<Node> Child = new List<Node>();
            internal Node Parent;
            internal int bitInx;
            internal BigInteger Data;
            internal uint Reference;
            internal ushort IdxLeft;
            internal ushort IdxRight;
            internal string Key;

            internal Node()
            {
                Child.Add(this);
                Child.Add(this);
                Reference = UInt32.MaxValue;
            }
            internal string GetName()
            {
                BigInteger data = BitLength(Data) + 7 / 8;
                byte[] stringBytes = Data.ToByteArray();
                Array.Reverse(stringBytes, 0, stringBytes.Length); //Convert to big endian
                return Encoding.UTF8.GetString(stringBytes); //Decode byte[] to string
            }
            internal int GetCompactBitIdx()
            {
                int byteIndx = bitInx / 8;
                return (byteIndx << 3) | bitInx - 8 * byteIndx;
            }
            internal Node(BigInteger data, int bitidx, Node parent) : this()
            {
                Data = data;
                bitInx = bitidx;
                Parent = parent;
            }
            internal Node(string key) : this()
            {
                Key = key;
            }
        }
    }
}
