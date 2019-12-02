/*
 *  Liquid Fast Infoset - XML Compression Library
 *  Copyright © 2001-2011 Liquid Technologies Limited. All rights reserved.
 *  
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU Affero General Public License as
 *  published by the Free Software Foundation, either version 3 of the
 *  License, or (at your option) any later version.
 *  
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU Affero General Public License for more details.
 *  
 *  You should have received a copy of the GNU Affero General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *  
 *  For product and commercial licensing details please contact us:
 *  http://www.liquid-technologies.com
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace LiquidTechnologies.FastInfoset
{
    // NOTE: Encoder does not validate order of method calls,
    // it is therefore up to the calling class to ensure valid state at all times
    internal class FIEncoder
    {
        #region Consts
        private const byte DEFAULT_MAX_ADD_LEN = 60;
        #endregion

        #region Inner Classes
        internal struct FIAttribute
        {
            internal void Init(string prefix, string ns, string localName)
            {
                encoding = null;
                data = null;
                qnameIndex.Init(prefix, ns, localName);
            }

            internal FIWriterVocabulary.QNameIndex qnameIndex;
            internal FIEncoding encoding;
            internal object data;
        }

        internal class FIElement
        {
            private const int GROW_SIZE = 10;

            internal FIElement()
            {
                _defaultNamespace = null; // must be null as empty string has meaning
                _nsAttributesTop = -1;
                _nsAttributes = new FIAttribute[GROW_SIZE];
                _attributesTop = -1;
                _attributes = new FIAttribute[GROW_SIZE];
            }

            internal void Init(string prefix, string ns, string localName)
            {
                _defaultNamespace = null; // must be null as empty string has meaning
                _nsAttributesTop = -1;
                _attributesTop = -1;
                _qnameIndex.Init(prefix, ns, localName);
            }

            internal void AddAttribute(FIAttribute attribute)
            {
                if (_attributesTop == (_attributes.Length - 1))
                {
                    FIAttribute[] destinationArray = new FIAttribute[_attributes.Length + GROW_SIZE];
                    if (_attributesTop > 0)
                        Array.Copy(_attributes, destinationArray, _attributesTop + 1);

                    _attributes = destinationArray;
                }

                _attributesTop++;
                _attributes[_attributesTop] = attribute;
            }

            internal void AddNamespaceAttribute(FIAttribute nsAttribute)
            {
                if (_nsAttributesTop == (_nsAttributes.Length - 1))
                {
                    FIAttribute[] destinationArray = new FIAttribute[_nsAttributes.Length + GROW_SIZE];
                    if (_nsAttributesTop > 0)
                        Array.Copy(_nsAttributes, destinationArray, _nsAttributesTop + 1);

                    _nsAttributes = destinationArray;
                }

                _nsAttributesTop++;
                _nsAttributes[_nsAttributesTop] = nsAttribute;
            }

            internal string DefaultNamespace
            {
                get { return _defaultNamespace; }
                set { _defaultNamespace = value; }
            }

            internal FIAttribute[] Attributes
            {
                get { return _attributes; }
            }

            internal int AttributeCount
            {
                get { return _attributesTop + 1; }
            }

            internal FIAttribute[] NamespaceAttributes
            {
                get { return _nsAttributes; }
            }

            internal int NamespaceAttributeCount
            {
                get { return _nsAttributesTop + 1; }
            }

            internal FIWriterVocabulary.QNameIndex QNameIndex
            {
                get { return _qnameIndex; }
            }

            private FIWriterVocabulary.QNameIndex _qnameIndex;
            private string _defaultNamespace;
            private FIAttribute[] _nsAttributes;
            private int _nsAttributesTop;
            private FIAttribute[] _attributes;
            private int _attributesTop;
        }
        #endregion

        #region Constructors
        internal FIEncoder(Stream output, FIWriterVocabulary vocabulary)
        {
            _output = output;
            if (vocabulary != null)
                // copy initial vocabulary
                _vocabulary = new FIWriterVocabulary(vocabulary);

            _terminateElement = false;
            _terminateAttributes = false;
            _terminateDTD = false;

            _encodingBuffer = new byte[1024]; // 1K
            _encodingBufferLength = 0;
        }
        #endregion

        #region Internal Interface
        internal void Close()
        {
            if (_output != null)
                _output.Close();

            _output = null;
        }

        internal void Flush()
        {
            if (_output != null)
                _output.Flush();
        }

        internal FIWriterVocabulary Vocabulary
        {
            get { return _vocabulary; }
        }

        internal void WriteDeclaraion(FIWriter.FInfoDecl decl)
        {
            // append declaration
            if (decl != FIWriter.FInfoDecl.FInfoDecl_NONE)
                // already converted to UTF-8
                _output.Write(FIConsts.FI_DECLARATION_LIST[(int)decl - 1], 0, FIConsts.FI_DECLARATION_LIST[(int)decl - 1].Length);
        }

        internal void WriteHeader()
        {
            // append FastInfoset magic number and version
            byte[] header = { 0xE0, 0, 0, 0x01 };
            _output.Write(header, 0, 4);

            // append optional components
            if (_vocabulary == null)
            {
                // no optional components
                _vocabulary = new FIWriterVocabulary();
                _output.WriteByte((byte)0x0);
            }
            else
                WriteOptionalComponents();
        }

        private void WriteOptionalComponents()
        {
            // initial vocabulary present so append x0100000
            _output.WriteByte((byte)0x20);

            if (_vocabulary.URI != null)
            {
                // external vocabulary only so append 00010000, 00000000, and 1 bit padding
                _output.WriteByte((byte)0x10);
                _output.WriteByte((byte)0x0);
                NonEmptyOctetStringBit2(0, _vocabulary.URI);
            }
            else
            {
                // TODO:
                throw new Exception("Initial Vocabularies are not currently supported");
            }
        }

        internal void WriteElement(FIElement element)
        {
            AlignmentOnBit1();

            // write element starting on bit 2 (bit 1 is padding)...

            byte byCurrent = 0;

            // bit 2 is set if has Attributes
            if (element.AttributeCount != 0)
                byCurrent = 0x40;

            string defaultNamespace = element.DefaultNamespace;
            if ((defaultNamespace != null) || (element.NamespaceAttributeCount != 0))
            {
                // we have namespace-attributes so append xx1110 and 00 padding
                _output.WriteByte((byte)(byCurrent | 0x38));
                byCurrent = 0;

                // for each NamespaceAttribute append 110011 identifier
                // append 1 if has prefix else 0
                // append 1 if has namespace-name else 0

                if (defaultNamespace != null)
                {
                    // no prefix, so append 11001101
                    _output.WriteByte((byte)0xCD);
                    IdentifyingStringOrIndexBit1(defaultNamespace, _vocabulary.NamespaceNamesMap);
                }

                // add Namespace Attributes
                for (int n = 0; n < element.NamespaceAttributeCount; n++)
                {
                    FIAttribute attr = element.NamespaceAttributes[n];

                    // must have prefix, so append 11001111
                    _output.WriteByte((byte)0xCF);
                    IdentifyingStringOrIndexBit1(attr.qnameIndex.qname.localName, _vocabulary.PrefixNamesMap);
                    IdentifyingStringOrIndexBit1((string)(attr.data), _vocabulary.NamespaceNamesMap);
                }

                // add terminator after all namespace-attributes
                _output.WriteByte(FIConsts.FI_TERMINATOR);

                // 2 bits padding are required
            }

            // always on bit 3...

            // add Qualified Name
            QualifiedNameOrIndexBit3(byCurrent, element.QNameIndex, _vocabulary.ElementNamesMap);

            // add Attributes
            for (int n = 0; n < element.AttributeCount; n++)
            {
                FIAttribute attr = element.Attributes[n];

                QualifiedNameOrIndexBit2(0, attr.qnameIndex, _vocabulary.AttributeNamesMap);

                if (attr.data != null)
                {
                    if (attr.encoding != null)
                        // write attribute text as encoded characters
                        NonIdentifyingStringOrIndexBit1(attr.encoding, attr.data);
                    else if (attr.data is string)
                        // write attribute text as string
                        NonIdentifyingStringOrIndexBit1((string)attr.data, _vocabulary.AttributeValuesMap);
                    else
                        throw new LtFastInfosetException("Internal Error. Invalid data in WriteEndAttribute");
                }
            }

            // terminate attributes
            _terminateAttributes = (element.AttributeCount > 0);
        }

        internal void WriteEndElement()
        {
            Debug.Assert(!_terminateDTD);

            // if we are closing an element and it's parent, we need a double terminator (11111111)
            if (_terminateElement || _terminateAttributes)
            {
                _output.WriteByte(FIConsts.FI_DOUBLE_TERMINATOR);
                _terminateElement = false;
                _terminateAttributes = false;
            }
            else
                _terminateElement = true;
        }

        internal void WriteEndDocument()
        {
            // we need to add 2 terminators, one for end of 'children' and one for end of 'document'
            if (_terminateElement || _terminateDTD)
                _output.WriteByte(FIConsts.FI_DOUBLE_TERMINATOR);
            else
                _output.WriteByte(FIConsts.FI_TERMINATOR);
        }

        internal void WriteProcessingInstruction(string target, string content)
        {
            AlignmentOnBit1();

            // write PI identifier 11100001
            _output.WriteByte((byte)0xE1);
            IdentifyingStringOrIndexBit1(target, _vocabulary.OtherNCNamesMap);
            NonIdentifyingStringOrIndexBit1(content, _vocabulary.OtherStringMap);
        }

        internal void WriteComment(string text)
        {
            AlignmentOnBit1();
            // write Comment identifier 11100010
            _output.WriteByte((byte)0xE2);
            NonIdentifyingStringOrIndexBit1(text, _vocabulary.OtherStringMap);
        }

        internal void WriteDocumentTypeDeclaration(string name, string pubid, string sysid, string subset)
        {
            // TODO:
            throw new LtFastInfosetException("WriteDocumentTypeDeclaration not supported");

            // AlignmentOnBit1();
            // _terminateDTD = true;
        }

        internal void WriteUnexpandedEntityReference()
        {
            // TODO:
            throw new LtFastInfosetException("WriteUnexpandedEntityReference not supported");

            // AlignmentOnBit1();
        }

        internal void WriteCharacterChunk(string text)
        {
            AlignmentOnBit1();

            // append 10xxxxxx
            NonIdentifyingStringOrIndexBit3(0x80, text, _vocabulary.ContentCharacterChunksMap);
        }

        internal void WriteCharacterChunk(FIEncoding encoding, object data)
        {
            AlignmentOnBit1();

            // append 10xxxxxx
            NonIdentifyingStringOrIndexBit3(0x80, encoding, data);
        }
        #endregion

        #region Private Methods
        private void AlignmentOnBit1()
        {
            if (_terminateElement || _terminateAttributes || _terminateDTD)
            {
                _output.WriteByte(FIConsts.FI_TERMINATOR);
                _terminateElement = false;
                _terminateAttributes = false;
                _terminateDTD = false;
            }
        }
        #endregion

        #region Private FI Encoding Methods
        // C.13
        private void IdentifyingStringOrIndexBit1(string key, Dictionary<string, int> mapValues)
        {
            int index;
            if (mapValues.TryGetValue(key, out index))
            {
                // string-index so add 1xxxxxxx and index
                Integer1To2pw20Bit2(0x80, index);
            }
            else
            {
                // literal-character-string
                NonEmptyOctetStringBit2(0, key);
                if (mapValues.Count < FIConsts.TWO_POWER_TWENTY)
                {
                    mapValues.Add(key, mapValues.Count + 1);
                }
            }
        }

        // C.14
        private void NonIdentifyingStringOrIndexBit1(string key, Dictionary<string, int> mapValues)
        {
            if (key.Length == 0)
            {
                // just write 0, which means empty string
                Integer0To2pw20Bit2(0x80, 0);
            }
            else if (key.Length < DEFAULT_MAX_ADD_LEN)
            {
                int index;
                if (mapValues.TryGetValue(key, out index))
                {
                    // string-index so add 1xxxxxxx and index
                    Integer1To2pw20Bit2(0x80, index);
                }
                else
                {
                    // literal-character-string, add-to-table so add 01xxxxxx
                    EncodedCharacterStringBit3(0x40, key);
                    if (mapValues.Count < FIConsts.TWO_POWER_TWENTY)
                        mapValues.Add(key, mapValues.Count + 1);
                }
            }
            else
            {
                // literal-character-string, no add-to-table so add 00xxxxxx
                EncodedCharacterStringBit3(0, key);
            }
        }

        private void NonIdentifyingStringOrIndexBit1(FIEncoding encoding, object data)
        {
            // literal-character-string, no add-to-table so add 00xxxxxx
            EncodedCharacterStringBit3(0, encoding, data);
        }

        // C.15
        private void NonIdentifyingStringOrIndexBit3(byte byCurrent, string key, Dictionary<string, int> mapValues)
        {
            Debug.Assert(key.Length > 0);

            if (key.Length < DEFAULT_MAX_ADD_LEN)
            {
                int index;
                if (mapValues.TryGetValue(key, out index))
                {
                    // string-index so add xx1xxxxx and index
                    Integer1To2pw20Bit4((byte)(byCurrent | 0x20), index);
                }
                else
                {
                    // literal-character-string and add-to-table so xx01xxxx
                    EncodedCharacterStringBit5((byte)(byCurrent | 0x10), key);
                    if (mapValues.Count < FIConsts.TWO_POWER_TWENTY)
                    {
                        mapValues.Add(key, mapValues.Count + 1);
                    }
                }
            }
            else
            {
                // literal-character-string, no add-to-table xx00xxxx
                EncodedCharacterStringBit5(byCurrent, key);
            }
        }

        private void NonIdentifyingStringOrIndexBit3(byte byCurrent, FIEncoding encoding, object data)
        {
            // literal-character-string, no add-to-table xx00xxxx
            EncodedCharacterStringBit5(byCurrent, encoding, data);
        }

        // C.17
        private void QualifiedNameOrIndexBit2(byte byCurrent, FIWriterVocabulary.QNameIndex qnameIndex, FIWriterVocabulary.QNameArray mapQNames)
        {
            // byCurrent should only have most significant bits preset x0000000
            Debug.Assert((byCurrent & 0x7F) == 0);
            Debug.Assert((qnameIndex.qname.localName != null) && (qnameIndex.qname.localName.Length != 0));

            int index = 0;
            if (!mapQNames.TryAddQName(qnameIndex, out index))
            {
                // existing name-surrogate-index
                Integer1To2pw20Bit2(byCurrent, index);
            }
            else
            {
                // literal-qualified-name so append x11110xx

                // see if we have prefix and namespace
                if ((qnameIndex.qname.prefix != null) && (qnameIndex.qname.prefix.Length != 0))
                {
                    // must have namespace if we have prefix
                    Debug.Assert(qnameIndex.qname.ns.Length != 0);
                    // prefix and namespace x1111011
                    _output.WriteByte((byte)(byCurrent | 0x7B));
                }
                else if ((qnameIndex.qname.ns != null) && (qnameIndex.qname.ns.Length != 0))
                    // namespace x1111001
                    _output.WriteByte((byte)(byCurrent | 0x79));
                else
                    // neither x1111000
                    _output.WriteByte((byte)(byCurrent | 0x78));


                // always on bit 1 here...

                if ((qnameIndex.qname.prefix != null) && (qnameIndex.qname.prefix.Length != 0))
                {
                    int prefixIndex = 0;
                    if (_vocabulary.FindPrefixNameIndex(qnameIndex.qname.prefix, out prefixIndex))
                    {
                        // string-index so add 1xxxxxxx and index
                        Integer1To2pw20Bit2(0x80, prefixIndex);
                    }
                    else
                    {
                        // literal-character-string
                        NonEmptyOctetStringBit2(0, qnameIndex.qname.prefix);
                        _vocabulary.AddPrefixName(qnameIndex.qname.prefix);
                    }
                }

                if ((qnameIndex.qname.ns != null) && (qnameIndex.qname.ns.Length != 0))
                {
                    int namespaceIndex = 0;
                    if (_vocabulary.FindNamespaceNameIndex(qnameIndex.qname.ns, out namespaceIndex))
                    {
                        // string-index so add 1xxxxxxx and index
                        Integer1To2pw20Bit2(0x80, namespaceIndex);
                    }
                    else
                    {
                        // literal-character-string
                        NonEmptyOctetStringBit2(0, qnameIndex.qname.ns);
                        _vocabulary.AddNamespaceName(qnameIndex.qname.ns);
                    }
                }

                int localNameIndex = 0;
                if (_vocabulary.FindLocalNameIndex(qnameIndex.qname.localName, out localNameIndex))
                {
                    // string-index so add 1xxxxxxx and index
                    Integer1To2pw20Bit2(0x80, localNameIndex);
                }
                else
                {
                    // literal-character-string
                    NonEmptyOctetStringBit2(0, qnameIndex.qname.localName);
                    _vocabulary.AddLocalName(qnameIndex.qname.localName);
                }
            }
        }

        // C.18
        private void QualifiedNameOrIndexBit3(byte byCurrent, FIWriterVocabulary.QNameIndex qnameIndex, FIWriterVocabulary.QNameArray mapQNames)
        {
            // byCurrent should only have 2 most significant bits preset xx000000
            Debug.Assert((byCurrent & 0x3F) == 0);
            Debug.Assert((qnameIndex.qname.localName != null) && (qnameIndex.qname.localName.Length != 0));

            int index = 0;
            if (!mapQNames.TryAddQName(qnameIndex, out index))
            {
                // existing name-surrogate-index
                Integer1To2pw20Bit3(byCurrent, index);
            }
            else
            {
                // literal-qualified-name so append xx1111xx

                // see if we have prefix and namespace
                if ((qnameIndex.qname.prefix != null) && (qnameIndex.qname.prefix.Length != 0))
                {
                    // must have namespace if we have prefix
                    Debug.Assert(qnameIndex.qname.ns.Length != 0);
                    // prefix and namespace xx111111
                    _output.WriteByte((byte)(byCurrent | 0x3F));
                }
                else if ((qnameIndex.qname.ns != null) && (qnameIndex.qname.ns.Length != 0))
                    // namespace xx111101
                    _output.WriteByte((byte)(byCurrent | 0x3D));
                else
                    // neither xx111100
                    _output.WriteByte((byte)(byCurrent | 0x3C));


                // always on bit 1 here...

                if ((qnameIndex.qname.prefix != null) && (qnameIndex.qname.prefix.Length != 0))
                {
                    int prefixIndex = 0;
                    if (_vocabulary.FindPrefixNameIndex(qnameIndex.qname.prefix, out prefixIndex))
                    {
                        // string-index so add 1xxxxxxx and index
                        Integer1To2pw20Bit2(0x80, prefixIndex);
                    }
                    else
                    {
                        // literal-character-string
                        NonEmptyOctetStringBit2(0, qnameIndex.qname.prefix);
                        _vocabulary.AddPrefixName(qnameIndex.qname.prefix);
                    }
                }

                if ((qnameIndex.qname.ns != null) && (qnameIndex.qname.ns.Length != 0))
                {
                    int namespaceIndex = 0;
                    if (_vocabulary.FindNamespaceNameIndex(qnameIndex.qname.ns, out namespaceIndex))
                    {
                        // string-index so add 1xxxxxxx and index
                        Integer1To2pw20Bit2(0x80, namespaceIndex);
                    }
                    else
                    {
                        // literal-character-string
                        NonEmptyOctetStringBit2(0, qnameIndex.qname.ns);
                        _vocabulary.AddNamespaceName(qnameIndex.qname.ns);
                    }
                }

                int localNameIndex = 0;
                if (_vocabulary.FindLocalNameIndex(qnameIndex.qname.localName, out localNameIndex))
                {
                    // string-index so add 1xxxxxxx and index
                    Integer1To2pw20Bit2(0x80, localNameIndex);
                }
                else
                {
                    // literal-character-string
                    NonEmptyOctetStringBit2(0, qnameIndex.qname.localName);
                    _vocabulary.AddLocalName(qnameIndex.qname.localName);
                }
            }
        }

        // C.19
        private void EncodedCharacterStringBit3(byte byCurrent, string val)
        {
            switch (_vocabulary.CharacterStringEncoding)
            {
                case FIWriterVocabulary.StringEncoding.UTF8:
                    {
                        EncodeAsUTF8(val);

                        // UTF8 Encoding, so xx00xxxx
                        NonEmptyOctetStringBit5(byCurrent, _encodingBuffer, _encodingBufferLength);
                        break;
                    }
                case FIWriterVocabulary.StringEncoding.UTF16BE:
                    {
                        EncodeAsUTF16BE(val);

                        // UNICODE Encoding, so xx01xxxx
                        NonEmptyOctetStringBit5((byte)(byCurrent & 0x10), _encodingBuffer, _encodingBufferLength);
                        break;
                    }
                default:
                    throw new LtFastInfosetException("Internal Error in EncodedCharacterStringBit3. Unknown StringEncoding: " + _vocabulary.CharacterStringEncoding.ToString());
            }
        }

        private void EncodedCharacterStringBit3(byte byCurrent, FIEncoding encoding, object data)
        {
            int tableIndex = encoding.TableIndex;

            if (tableIndex < FIConsts.ENCODING_TABLE_MIN || tableIndex > FIConsts.ENCODING_TABLE_MAX)
                throw new LtFastInfosetException("Invalid Encoded Character table index [" + tableIndex + "]");

            byte byVal = (byte)(tableIndex - 1);

            byte[] buffer = encoding.Encode(data);

            if (encoding is FIRestrictedAlphabet)
                // restricted-alphabet, so xx10xxxx
                _output.WriteByte((byte)(byCurrent | 0x20 | byVal >> 4));
            else if ((encoding is FIEncodingAlgorithm) || (encoding is InternalEncodingAlgorithm))
                // encoding-algorithm, so xx11xxxx
                _output.WriteByte((byte)(byCurrent | 0x30 | byVal >> 4));
            else
                throw new LtFastInfosetException("Internal Error in EncodedCharacterStringBit3. Unknown encoding type.");

            NonEmptyOctetStringBit5((byte)(byVal << 4), buffer, buffer.Length);
        }

        // C.20
        private void EncodedCharacterStringBit5(byte byCurrent, string val)
        {
            switch (_vocabulary.CharacterStringEncoding)
            {
                case FIWriterVocabulary.StringEncoding.UTF8:
                    {
                        EncodeAsUTF8(val);

                        // UTF8 Encoding, so xxxx00xx
                        NonEmptyOctetStringBit7(byCurrent, _encodingBuffer, _encodingBufferLength);
                        break;
                    }
                case FIWriterVocabulary.StringEncoding.UTF16BE:
                    {
                        EncodeAsUTF16BE(val);

                        // UNICODE Encoding, so xxxx01xx
                        NonEmptyOctetStringBit7((byte)(byCurrent & 0x04), _encodingBuffer, _encodingBufferLength);
                        break;
                    }
                default:
                    throw new LtFastInfosetException("Internal Error in EncodedCharacterStringBit5. Unknown StringEncoding: " + _vocabulary.CharacterStringEncoding.ToString());
            }
        }

        private void EncodedCharacterStringBit5(byte byCurrent, FIEncoding encoding, object data)
        {
            int tableIndex = encoding.TableIndex;

            if (tableIndex < FIConsts.ENCODING_TABLE_MIN || tableIndex > FIConsts.ENCODING_TABLE_MAX)
                throw new LtFastInfosetException("Invalid Encoded Character table index [" + tableIndex + "]");

            byte byVal = (byte)(tableIndex - 1);

            byte[] buffer = encoding.Encode(data);

            if (encoding is FIRestrictedAlphabet)
                // restricted-alphabet, so xxxx10xx
                _output.WriteByte((byte)(byCurrent | 0x08 | byVal >> 6));
            else if ((encoding is FIEncodingAlgorithm) || (encoding is InternalEncodingAlgorithm))
                // encoding-algorithm, so xxxx11xx
                _output.WriteByte((byte)(byCurrent | 0x0C | byVal >> 6));
            else
                throw new LtFastInfosetException("Internal Error in EncodedCharacterStringBit3. Unknown encoding type.");

            NonEmptyOctetStringBit7((byte)(byVal << 2), buffer, buffer.Length);
        }

        // C.22
        private void NonEmptyOctetStringBit2(byte byCurrent, string val)
        {
            EncodeAsUTF8(val);

            // TODO: this will fail fo values > 2^31
            int len = _encodingBufferLength;

            if (len < 65)
            {
                // 1 to 64
                _output.WriteByte((byte)(byCurrent | (byte)(len - 1)));
            }
            else if (len < 321)
            {
                // 65 to 320
                _output.WriteByte((byte)(byCurrent | 0x40));
                _output.WriteByte((byte)(len - 65));
            }
            else
            {
                // 321 to 2^32
                int add = len - 321;
                _output.WriteByte((byte)(byCurrent | 0x60));
                _output.WriteByte((byte)(add >> 24));
                _output.WriteByte((byte)(add >> 16));
                _output.WriteByte((byte)(add >> 8));
                _output.WriteByte((byte)add);
            }

            _output.Write(_encodingBuffer, 0, len);
        }

        // C.23
        private void NonEmptyOctetStringBit5(byte byCurrent, byte[] data, int len)
        {
            // TODO: this will fail fo values > 2^31

            if (len < 9)
            {
                // 1 to 8
                _output.WriteByte((byte)(byCurrent | (byte)(len - 1)));
            }
            else if (len < 265)
            {
                // 9 to 264
                _output.WriteByte((byte)(byCurrent | 0x08));
                _output.WriteByte((byte)(len - 9));
            }
            else
            {
                // 265 to 2^32
                int add = len - 265;
                _output.WriteByte((byte)(byCurrent | 0x0C));
                _output.WriteByte((byte)(add >> 24));
                _output.WriteByte((byte)(add >> 16));
                _output.WriteByte((byte)(add >> 8));
                _output.WriteByte((byte)add);
            }

            _output.Write(data, 0, len);
        }

        // C.24
        private void NonEmptyOctetStringBit7(byte byCurrent, byte[] data, int len)
        {
            // TODO: this will fail fo values > 2^31

            if (len < 3)
            {
                // 1 to 2
                _output.WriteByte((byte)(byCurrent | (byte)(len - 1)));
            }
            else if (len < 259)
            {
                // 3 to 258
                _output.WriteByte((byte)(byCurrent | 0x02));
                _output.WriteByte((byte)(len - 3));
            }
            else
            {
                // 259 to 2^32
                int add = len - 259;
                _output.WriteByte((byte)(byCurrent | 0x03));
                _output.WriteByte((byte)(add >> 24));
                _output.WriteByte((byte)(add >> 16));
                _output.WriteByte((byte)(add >> 8));
                _output.WriteByte((byte)add);
            }

            _output.Write(data, 0, len);
        }

        // C.25
        private void Integer1To2pw20Bit2(byte byCurrent, int val)
        {
            Debug.Assert(val > 0);

            if (val < 65)
            {
                // 1 to 64
                _output.WriteByte((byte)(byCurrent | (byte)(val - 1)));
            }
            else if (val < 8257)
            {
                // 65 to 8256
                int add = val - 65;
                _output.WriteByte((byte)(byCurrent | 0x40 | (byte)((add >> 8) & 0x1F)));
                _output.WriteByte(((byte)add));
            }
            else
            {
                // 8257 to 2^20
                int add = val - 8257;
                _output.WriteByte((byte)(byCurrent | 0x60 | (byte)((add >> 16) & 0x0F)));
                _output.WriteByte((byte)(add >> 8));
                _output.WriteByte((byte)add);
            }
        }

        // C.26
        private void Integer0To2pw20Bit2(byte byCurrent, int val)
        {
            if (val == 0)
                _output.WriteByte((byte)(byCurrent | 0x7F));
            else
                Integer1To2pw20Bit2(byCurrent, val);
        }

        // C.27
        private void Integer1To2pw20Bit3(byte byCurrent, int val)
        {
            Debug.Assert(val > 0);

            if (val < 33)
            {
                // 1 to 32
                _output.WriteByte((byte)(byCurrent | (byte)(val - 1)));
            }
            else if (val < 2081)
            {
                // 33 to 2080
                int add = val - 33;
                _output.WriteByte((byte)(byCurrent | 0x20 | (byte)((add >> 8) & 0x07)));
                _output.WriteByte((byte)add);
            }
            else if (val < 526369)
            {
                // 2081 to 526368
                int add = val - 2081;
                _output.WriteByte((byte)(byCurrent | 0x28 | (byte)((add >> 16) & 0x07)));
                _output.WriteByte((byte)(add >> 8));
                _output.WriteByte((byte)add);
            }
            else
            {
                // 526368 to 2^20
                int add = val - 526368;
                _output.WriteByte((byte)(byCurrent | 0x30));
                _output.WriteByte((byte)((add >> 16) & 0x0F));
                _output.WriteByte((byte)(add >> 8));
                _output.WriteByte((byte)add);
            }
        }

        // C.28
        private void Integer1To2pw20Bit4(byte byCurrent, int val)
        {
            Debug.Assert(val > 0);

            if (val < 17)
            {
                // 1 to 16
                _output.WriteByte((byte)(byCurrent | (byte)(val - 1)));
            }
            else if (val < 1041)
            {
                // 17 to 1040
                int add = val - 17;
                _output.WriteByte((byte)(byCurrent | 0x10 | (byte)((add >> 8) & 0x03)));
                _output.WriteByte((byte)add);
            }
            else if (val < 263185)
            {
                // 1041 to 263184
                int add = val - 1041;
                _output.WriteByte((byte)(byCurrent | 0x14 | (byte)((add >> 16) & 0x03)));
                _output.WriteByte((byte)(add >> 8));
                _output.WriteByte((byte)add);
            }
            else
            {
                // 263185 to 2^20
                int add = val - 263185;
                _output.WriteByte((byte)(byCurrent | 0x18));
                _output.WriteByte((byte)((add >> 16) & 0x0F));
                _output.WriteByte((byte)(add >> 8));
                _output.WriteByte((byte)add);
            }
        }

        private void EncodeAsUTF8(string val)
        {
            int maxRequired = val.Length * 4;
            if (_encodingBuffer.Length < maxRequired)
                _encodingBuffer = new byte[maxRequired];

            _encodingBufferLength = Encoding.UTF8.GetBytes(val, 0, val.Length, _encodingBuffer, 0);
        }

        private void EncodeAsUTF16BE(string val)
        {
            int maxRequired = val.Length * 2;
            if (_encodingBuffer.Length < maxRequired)
                _encodingBuffer = new byte[maxRequired];

            _encodingBufferLength = Encoding.BigEndianUnicode.GetBytes(val, 0, val.Length, _encodingBuffer, 0);
        }
        #endregion

        #region Member Variables
        private Stream _output;
        private FIWriterVocabulary _vocabulary;
        private bool _terminateElement;
        private bool _terminateAttributes;
        private bool _terminateDTD;
        private byte[] _encodingBuffer = null;
        private int _encodingBufferLength = 0;
        #endregion
    }
}
