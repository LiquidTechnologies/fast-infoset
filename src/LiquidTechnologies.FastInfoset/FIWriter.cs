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
using System.Xml;

namespace LiquidTechnologies.FastInfoset
{
    /// <summary>
    /// Represents a writer that provides a fast, non-cached, forward-only way of generating streams or files containing XML binary encoded data that conforms to the ITU-T: Fast infoset X.891 (05/2005) recommendation.
    /// </summary>
    /// <remarks><para>The FIWriter class derives from the System.Xml.XmlWriter .Net Framework class and supports the abstract methods required to integrate with other .Net Framework XML classes such as System.Xml.XmlDocument.</para>
    /// <note><para>In the Microsoft .NET Framework version 2.0 release, the recommended practice is to create XmlWriter instances using the System.Xml.XmlWriter.Create method and the XmlWriterSettings class. This allows you to take full advantage of all the new features introduced in this release. For more information, see Creating XML Writers.</para></note>
    /// </remarks>
    /// <seealso cref="FIReader"/>
    /// <example>
    /// <code>
    /// string filename1 = new string(@"c:\MyFile.xml");
    /// XmlDocument doc = new XmlDocument();
    /// 
    /// // Read standard XML file into an XmlDocument
    /// XmlReader reader = XmlReader.Create(new XmlTextReader(filename1), null);
    /// doc.Load(reader);
    /// reader.Close();
    /// 
    /// // Write XML to file encoded as Fast Infoset
    /// XmlWriter fiWriter = XmlWriter.Create(new FIWriter(filename1 + ".finf"));
    /// doc.WriteTo(fiWriter);
    /// fiWriter.Close();
    /// 
    /// string filename2 = new string(@"c:\MyFile2.xml");
    /// XmlDocument doc2 = new XmlDocument();
    /// 
    /// // Read Fast Infoset encoded XML file into an XmlDocument
    /// XmlReader fiReader = XmlReader.Create(new FIReader(filename1 + ".finf"), null);
    /// doc2.Load(fiReader);
    /// fiReader.Close();
    /// 
    /// // Write standard XML to file
    /// XmlWriter writer = XmlWriter.Create(new XmlTextWriter(filename2, Encoding.Default));
    /// doc2.WriteTo(writer);
    /// writer.Close();
    /// </code>
    /// </example>
    public sealed class FIWriter : XmlWriter
    {
        #region Enums
        /// <summary>
        /// All possible XML Declarations that can be used in a Fast Infoset
        /// </summary>
        public enum FInfoDecl
        {
            ///	<summary>No Declaration</summary>
            FInfoDecl_NONE,
            ///	<summary>&lt;?xml encoding='finf'?&gt;</summary>
            FInfoDecl_1,
            ///	<summary>&lt;?xml encoding='finf' standalone='yes'?&gt;</summary>
            FInfoDecl_2,
            ///	<summary>&lt;?xml encoding='finf' standalone='no'?&gt;</summary>
            FInfoDecl_3,
            ///	<summary>&lt;?xml version='1.0' encoding='finf'?&gt;</summary>
            FInfoDecl_4,
            ///	<summary>&lt;?xml version='1.0' encoding='finf' standalone='yes'?&gt;</summary>
            FInfoDecl_5,
            ///	<summary>&lt;?xml version='1.0' encoding='finf' standalone='no'?&gt;</summary>
            FInfoDecl_6,
            ///	<summary>&lt;?xml version='1.1' encoding='finf'?&gt;</summary>
            FInfoDecl_7,
            ///	<summary>&lt;?xml version='1.1' encoding='finf' standalone='yes'?&gt;</summary>
            FInfoDecl_8,
            ///	<summary>&lt;?xml version='1.1' encoding='finf' standalone='no'?&gt;</summary>
            FInfoDecl_9
        };

        // NOTE: Order is important as it is offset in state table
        private enum FIItemType
        {
            Content = 0,
            Comment = 1,
            DocType = 2,
            EndAttribute = 3,
            EndDocument = 4,
            EndElement = 5,
            EntityRef = 6,
            FullEndElement = 7,
            ProcessingInstruction = 8,
            Raw = 9,
            StartAttribute = 10,
            StartDocument = 11,
            StartElement = 12,
            SurrogateCharEntity = 13,
            Whitespace = 14,
            EncodedContent = 15
        }

        // NOTE: Order is important as it is offset in state table
        private enum FIState
        {
            Start = 0,
            Prolog = 1,
            Element = 2,
            Attribute = 3,
            Content = 4,
            Closed = 5,
            Error = 6,
            AttributeContent = 7,
            Epilog = 8
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates an instance of the FIWriter class class using the specified file.
        /// </summary>
        /// <param name="filename">The file to which you want to write. If the file exists, it truncates it and overwrites it with the new content.</param>
        public FIWriter(string filename)
            : this(filename, null)
        {
        }

        /// <summary>
        /// Creates an instance of the FIWriter class class using the specified file and vocabulary.
        /// </summary>
        /// <param name="filename">The file to which you want to write. If the file exists, it truncates it and overwrites it with the new content.</param>
        /// <param name="vocabulary">The initial vocabulary used to provide the initial state of the internal vocabulary tables.</param>
        public FIWriter(string filename, FIVocabulary vocabulary)
            : this(new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.Read), vocabulary)
        {
        }

        /// <summary>
        /// Creates an instance of the XmlTextWriter class using the specified stream.
        /// </summary>
        /// <param name="output">The stream to which you want to write.</param>
        public FIWriter(Stream output)
            : this(output, null)
        {
        }

        /// <summary>
        /// Creates an instance of the XmlTextWriter class using the specified stream and vocabulary.
        /// </summary>
        /// <param name="output">The stream to which you want to write.</param>
        /// <param name="vocabulary">The initial vocabulary used to provide the initial state of the internal vocabulary tables.</param>
        public FIWriter(Stream output, FIVocabulary vocabulary)
        {
            if (vocabulary != null)
                _internalWriter = new InternalFIWriter(output, vocabulary.Writer);
            else
                _internalWriter = new InternalFIWriter(output, null);

            _state = FIState.Start;
            _depth = 0;
            _documentEnded = false;
        }
        #endregion

        #region Additional Interface Methods
        /// <summary>
        /// Writes a Fast Infoset declaration at the start of output stream.
        /// </summary>
        /// <param name="decl">The Fast Infoset Declaration to use.</param>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public void WriteStartDocument(FInfoDecl decl)
        {
            try
            {
                ValidateState(FIItemType.StartDocument);

                _internalWriter.WriteStartDocument(decl);

                UpdateState(FIItemType.StartDocument);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Writes the given text encoded using the specified Restricted Alphabet Encoding.
        /// </summary>
        /// <param name="text">Text to write.</param>
        /// <param name="alphabetTableIndex">Index of restricted alphabet</param>
        /// <exception cref="ArgumentException">A Restricted Alphabet cannot be found for the specified index.</exception>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        /// <exception cref="IndexOutOfRangeException">Index must be between 1 and 256</exception>
        public void WriteRestrictedAlphabetString(string text, int alphabetTableIndex)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                    return;

                if (alphabetTableIndex < FIConsts.ENCODING_TABLE_MIN || alphabetTableIndex > FIConsts.ENCODING_TABLE_MAX)
                    throw new IndexOutOfRangeException("alphabetTableIndex");

                ValidateState(FIItemType.Content);

                FIRestrictedAlphabet alphabet = _internalWriter.Vocabulary.RestrictedAlphabet(alphabetTableIndex);

                if (alphabet == null)
                    throw new ArgumentException("Index out of range.");

                _internalWriter.WriteEncodedData(alphabet, text);

                UpdateState(FIItemType.Content);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Writes the given data encoded using the specified Encoding Algorithm.
        /// </summary>
        /// <param name="data">Data to encode.</param>
        /// <param name="encodingAlgorithmURI">Encoding Algorithm Unique Identifier</param>
        /// <exception cref="ArgumentNullException">encodingAlgorithmURI is null.</exception>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        /// <exception cref="LtFastInfosetException">Cannot find EncodingAlgorithm for specified URI.</exception>
        public void WriteEncodedData(byte[] data, Uri encodingAlgorithmURI)
        {
            try
            {
                if ((data == null) || data.Length == 0)
                    return;

                if (encodingAlgorithmURI == null)
                    throw new ArgumentNullException("encodingAlgorithmURI");

                ValidateState(FIItemType.Content);

                FIEncoding algorithm = _internalWriter.Vocabulary.EncodingAlgorithm(encodingAlgorithmURI.ToString());
                if (algorithm == null)
                    throw new LtFastInfosetException("Cannot find EncodingAlgorithm for specified URI.");

                _internalWriter.WriteEncodedData(algorithm, data);

                UpdateState(FIItemType.Content);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }
        #endregion

        #region XmlWriter Overrides
        #region General Methods
        /// <summary>
        /// Closes this stream and the underlying stream.
        /// </summary>
        public override void Close()
        {
            try
            {
                if (!_documentEnded)
                    WriteEndDocument();
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
            finally
            {
                _internalWriter.Close();
                _state = FIState.Closed;
            }
        }

        /// <summary>
        ///  Flushes whatever is in the buffer to the underlying streams and also flushes the underlying stream.
        /// </summary>
        public override void Flush()
        {
            try
            {
                _internalWriter.Flush();
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Returns the closest prefix defined in the current namespace scope for the namespace URI.
        /// </summary>
        /// <param name="ns"></param>
        /// <returns></returns>
        public override string LookupPrefix(string ns)
        {
            try
            {
                return _internalWriter.LookupPrefix(ns);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Gets the state of the writer.
        /// </summary>
        public override WriteState WriteState
        {
            get
            {
                WriteState state = WriteState.Error;

                switch (_state)
                {
                    case FIState.Start:
                        state = WriteState.Start;
                        break;
                    case FIState.Prolog:
                        state = WriteState.Prolog;
                        break;
                    case FIState.Element:
                        state = WriteState.Element;
                        break;
                    case FIState.Attribute:
                        state = WriteState.Attribute;
                        break;
                    case FIState.Content:
                    case FIState.AttributeContent:
                    case FIState.Epilog:
                        state = WriteState.Content;
                        break;
                    case FIState.Closed:
                        state = WriteState.Closed;
                        break;
                    case FIState.Error:
                        state = WriteState.Error;
                        break;
                    default:
                        throw new LtFastInfosetException("Internal Error. Unknown State.");
                }

                return state;
            }
        }
        #endregion

        #region Write Content Methods
        /// <summary>
        /// Writes out the specified binary bytes using the built-in "base64" encoding algorithm [X.891 Section 10.3].
        /// </summary>
        /// <param name="buffer">Byte array to encode.</param>
        /// <param name="index">The position within the buffer indicating the start of the bytes to write.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public override void WriteBase64(byte[] buffer, int index, int count)
        {
            try
            {
                ValidateState(FIItemType.Content);

                if (_internalEncodingAlgorithm == null)
                    _internalEncodingAlgorithm = new InternalEncodingAlgorithm();

                _internalEncodingAlgorithm.Encoding = InternalEncodingAlgorithm.EncodingType.Base64Encoding;

                byte[] tempBuffer = new byte[count];
                Buffer.BlockCopy(buffer, index, tempBuffer, 0, count);

                _internalWriter.WriteEncodedData(_internalEncodingAlgorithm, tempBuffer);

                UpdateState(FIItemType.Content);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Writes out the specified string using the built-in "cdata" encoding algorithm [X.891 Section 10.11].
        /// </summary>
        /// <param name="text">Text to encode.</param>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public override void WriteCData(string text)
        {
            try
            {
                ValidateState(FIItemType.Content);

                if (_internalEncodingAlgorithm == null)
                    _internalEncodingAlgorithm = new InternalEncodingAlgorithm();

                _internalEncodingAlgorithm.Encoding = InternalEncodingAlgorithm.EncodingType.CDataEncoding;

                _internalWriter.WriteEncodedData(_internalEncodingAlgorithm, text);

                UpdateState(FIItemType.Content);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Writes out a Unicode character value.
        /// </summary>
        /// <param name="ch">Unicode character to write out.</param>
        /// <exception cref="ArgumentException">The character is in the surrogate pair character range, 0xd800 - 0xdfff.</exception>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public override void WriteCharEntity(char ch)
        {
            try
            {
                if ((ch >= 0xd800) && (ch <= 0xdfff))
                    throw new ArgumentException("The character is in the surrogate pair character range, 0xd800 - 0xdfff.");

                WriteString(ch.ToString());
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Writes out specified characters.
        /// </summary>
        /// <param name="buffer">Character array containing the text to write.</param>
        /// <param name="index">The position in the buffer indicating the start of the text to write.</param>
        /// <param name="count">The number of characters to write.</param>
        /// <exception cref="ArgumentNullException">buffer is a null reference (Nothing in Visual Basic).</exception>
        /// <exception cref="ArgumentOutOfRangeException">index or count is less than zero.<para>-or-</para>The buffer length minus index is less than count</exception>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public override void WriteChars(char[] buffer, int index, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("Invalid data");

            if ((buffer == null) || (index < 0) || (count < 0) || ((buffer.Length - index) < count))
                throw new ArgumentOutOfRangeException("Invalid data");

            WriteString(new string(buffer, index, count));
        }

        /// <summary>
        /// Writes out a comment [X.891 Section 7.8].
        /// </summary>
        /// <param name="text">Text to place inside the comment.</param>
        /// <exception cref="ArgumentException">The text would result in a non-well formed XML document.</exception>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public override void WriteComment(string text)
        {
            try
            {
                if (text != null)
                {
                    if (text.IndexOf("--", StringComparison.Ordinal) >= 0)
                        throw new ArgumentException("Comment text cannot contain '--'.");
                    else if ((text.Length != 0) && (text[text.Length - 1] == '-'))
                        throw new ArgumentException("Comment text cannot end with '-'.");
                }

                // if user didn't start document, do it for them
                if (_state == FIState.Start)
                    WriteStartDocument();

                ValidateState(FIItemType.Comment);

                _internalWriter.WriteComment(text);

                UpdateState(FIItemType.Comment);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// The method or operation is not implemented.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="pubid"></param>
        /// <param name="sysid"></param>
        /// <param name="subset"></param>
        /// <exception cref="Exception">The method or operation is not implemented.</exception>
        public override void WriteDocType(string name, string pubid, string sysid, string subset)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        /// <summary>
        /// The method or operation is not implemented.
        /// </summary>
        /// <param name="name"></param>
        /// <exception cref="Exception">The method or operation is not implemented.</exception>
        public override void WriteEntityRef(string name)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        /// <summary>
        /// Writes out a processing instruction [X.891 Section 7.5]
        /// </summary>
        /// <param name="name">Name of the processing instruction.</param>
        /// <param name="text">Text to include in the processing instruction.</param>
        /// <exception cref="ArgumentException"><para>The text would result in a non-well formed XML document.</para><para>name is either a null reference (Nothing in Visual Basic) or String.Empty.</para>This method is being used to create an XML declaration after <see cref="WriteStartDocument()"/> has already been called.</exception>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public override void WriteProcessingInstruction(string name, string text)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentException("Name cannot be null or empty.");

                if ((text != null) && (text.IndexOf("?>", StringComparison.Ordinal) >= 0))
                    throw new ArgumentException("Processing Instruction cannot contain '?>'.");

                if ((string.Compare(name, "xml", StringComparison.OrdinalIgnoreCase) == 0) && (_state != FIState.Start))
                    throw new ArgumentException("Processing Instruction 'xml' cannot be added after WriteStartDocument.");

                // if user didn't start document, do it for them
                if (_state == FIState.Start)
                    WriteStartDocument();

                ValidateState(FIItemType.ProcessingInstruction);

                _internalWriter.WriteProcessingInstruction(name, text);

                UpdateState(FIItemType.ProcessingInstruction);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Passes data to <see cref="WriteString"/>.
        /// </summary>
        /// <param name="data">Text to write.</param>
        public override void WriteRaw(string data)
        {
            WriteString(data);
        }

        /// <summary>
        /// Passes data to <see cref="WriteString"/>.
        /// </summary>
        /// <param name="buffer">Character array containing the text to write.</param>
        /// <param name="index">The position in the buffer indicating the start of the text to write.</param>
        /// <param name="count">The number of characters to write.</param>
        public override void WriteRaw(char[] buffer, int index, int count)
        {
            WriteString(new string(buffer, index, count));
        }

        /// <summary>
        /// Writes the given text content.
        /// </summary>
        /// <param name="text">Text to write.</param>
        public override void WriteString(string text)
        {
            try
            {
                ValidateState(FIItemType.Content);

                _internalWriter.WriteContent((text == null) ? "" : text);

                UpdateState(FIItemType.Content);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// The method or operation is not implemented.
        /// </summary>
        /// <param name="lowChar"></param>
        /// <param name="highChar"></param>
        /// <exception cref="Exception">The method or operation is not implemented.</exception>
        public override void WriteSurrogateCharEntity(char lowChar, char highChar)
        {
            throw new Exception("The method or operation is not implemented.");

            /*			if (((lowChar < 0xDC00) || (lowChar > 0xDFFF)) || ((highChar < 0xD800) || (highChar > 0xDBFF)))
                            throw new LtFastInfosetException("Invalid Surrogate Char Entity");

                        char[] buffer = new char[] { highChar, lowChar };

                        WriteChars(buffer, 0, 2);
            */
        }

        /// <summary>
        /// The method or operation is not implemented.
        /// </summary>
        /// <param name="ws"></param>
        public override void WriteWhitespace(string ws)
        {
            throw new Exception("The method or operation is not implemented.");
        }
        #endregion

        #region Write Attribute Methods
        /// <summary>
        /// Writes the start of an attribute.
        /// </summary>
        /// <param name="prefix">Namespace prefix of the attribute.</param>
        /// <param name="localName">LocalName of the attribute.</param>
        /// <param name="ns">NamespaceURI of the attribute.</param>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public override void WriteStartAttribute(string prefix, string localName, string ns)
        {
            try
            {
                // if user didn't end last attribute, do it for them
                if ((_state == FIState.Attribute) || (_state == FIState.AttributeContent))
                    WriteEndAttribute();

                ValidateState(FIItemType.StartAttribute);

                _internalWriter.WriteStartAttribute(prefix, localName, ns);

                UpdateState(FIItemType.StartAttribute);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Closes the previous <see cref="WriteStartAttribute"/> call.
        /// </summary>
        /// <remarks><para>If you call WriteStartAttribute, you can close the attribute with this method.</para>You can also close the attribute by calling <see cref="WriteStartAttribute"/> again, calling <see cref="XmlWriter.WriteAttributeString(string, string, string)"/>, calling <see cref="WriteEndElement"/>, or calling <see cref="WriteEndDocument"/>.</remarks>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public override void WriteEndAttribute()
        {
            try
            {
                ValidateState(FIItemType.EndAttribute);

                _internalWriter.WriteEndAttribute();

                UpdateState(FIItemType.EndAttribute);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }
        #endregion

        #region Write Document Methods
        /// <summary>
        /// Writes a Fast Infoset declaration at the start of output stream.
        /// </summary>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public override void WriteStartDocument()
        {
            try
            {
                ValidateState(FIItemType.StartDocument);

                _internalWriter.WriteStartDocument();

                UpdateState(FIItemType.StartDocument);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Writes a Fast Infoset declaration at the start of output stream.
        /// </summary>
        /// <param name="standalone">If true, it writes "standalone='yes'"; if false, it writes "standalone='no'".</param>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public override void WriteStartDocument(bool standalone)
        {
            try
            {
                ValidateState(FIItemType.StartDocument);

                _internalWriter.WriteStartDocument(standalone ? FInfoDecl.FInfoDecl_2 : FInfoDecl.FInfoDecl_3);

                UpdateState(FIItemType.StartDocument);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Closes any open elements or attributes and puts the writer back in the Start state.
        /// </summary>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public override void WriteEndDocument()
        {
            try
            {
                // end all open elements
                while (_depth > 0)
                    WriteEndElement();

                ValidateState(FIItemType.EndDocument);

                _internalWriter.WriteEndDocument();

                UpdateState(FIItemType.EndDocument);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }
        #endregion

        #region Write Element Methods
        /// <summary>
        /// Writes the specified start tag and associates it with the given namespace and prefix.
        /// </summary>
        /// <param name="prefix">The namespace prefix of the element.</param>
        /// <param name="localName">The local name of the element.</param>
        /// <param name="ns">The namespace URI to associate with the element. If this namespace is already in scope and has an associated prefix then the writer automatically writes that prefix also.</param>
        /// <exception cref="InvalidOperationException">The <see cref="WriteState"/> is invalid for this operation.</exception>
        public override void WriteStartElement(string prefix, string localName, string ns)
        {
            try
            {
                // if user didn't start document, do it for them
                if (_state == FIState.Start)
                    WriteStartDocument();

                ValidateState(FIItemType.StartElement);

                _internalWriter.WriteStartElement(prefix, localName, ns);

                UpdateState(FIItemType.StartElement);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Closes one element and pops the corresponding namespace scope. 
        /// </summary>
        public override void WriteEndElement()
        {
            try
            {
                // end attributes
                if (_state == FIState.Attribute)
                    WriteEndAttribute();

                ValidateState(FIItemType.EndElement);

                _internalWriter.WriteEndElement();

                UpdateState(FIItemType.EndElement);
            }
            catch (Exception ex)
            {
                _state = FIState.Error;
                throw ex;
            }
        }

        /// <summary>
        /// Closes one element and pops the corresponding namespace scope. 
        /// </summary>
        public override void WriteFullEndElement()
        {
            WriteEndElement();
        }
        #endregion
        #endregion

        #region Private Methods
        private void ValidateState(FIItemType itemType)
        {
            byte validState = StateTable[(byte)itemType, (byte)_state];

            if ((validState == 0) || _documentEnded)
            {
                FIState oldState = _state;
                _state = FIState.Error;
                throw new InvalidOperationException(string.Format("Invalid State [{0}]", oldState.ToString()));
            }
        }

        private void UpdateState(FIItemType itemType)
        {
            switch (itemType)
            {
                case FIItemType.StartDocument:
                    _state = FIState.Prolog;
                    break;
                case FIItemType.EndDocument:
                    _state = FIState.Start;
                    _documentEnded = true;
                    break;
                case FIItemType.StartElement:
                    _state = FIState.Element;
                    _depth++;
                    break;
                case FIItemType.EndElement:
                    _depth--;
                    if (_depth == 0)
                        _state = FIState.Epilog;
                    else
                        _state = FIState.Content;
                    break;
                case FIItemType.StartAttribute:
                    _state = FIState.Attribute;
                    break;
                case FIItemType.EndAttribute:
                    _state = FIState.Element;
                    break;
                case FIItemType.Content:
                case FIItemType.EncodedContent:
                    if ((_state == FIState.Attribute) || (_state == FIState.AttributeContent))
                        _state = FIState.AttributeContent;
                    else
                        _state = FIState.Content;
                    break;
            }
        }
        #endregion

        #region Member Variables
        private InternalFIWriter _internalWriter;
        private InternalEncodingAlgorithm _internalEncodingAlgorithm;
        private FIState _state;
        private int _depth;
        private bool _documentEnded;

        private static byte[,] StateTable = new byte[,]
		{
			//	Start	Prolog	Element	Attrib	Content	Closed	Error	AttrCnt Epilog
			{	0,		0,		1,		1,		1,		0,		0,		1,		0		},	// Content
			{	0,		1,		1,		0,		1,		0,		0,		0,		1		},	// Comment
			{	0,		1,		0,		0,		0,		0,		0,		0,		1		},	// DocType
			{	0,		0,		0,		1,		0,		0,		0,		1,		0		},	// EndAttribute
			{	0,		0,		0,		0,		0,		0,		0,		0,		1		},	// EndDocument
			{	0,		0,		1,		0,		1,		0,		0,		0,		0		},	// EndElement
			{	0,		0,		1,		1,		1,		0,		0,		0,		0		},	// EntityRef
			{	0,		0,		1,		0,		0,		0,		0,		0,		0		},	// FullEndElement
			{	0,		1,		1,		1,		1,		0,		0,		0,		1		},	// ProcessingInstruction
			{	0,		0,		1,		1,		1,		0,		0,		0,		0		},	// Raw
			{	0,		0,		1,		0,		0,		0,		0,		0,		0		},	// StartAttribute
			{	1,		0,		0,		0,		0,		0,		0,		0,		0		},	// StartDocument
			{	0,		1,		1,		0,		1,		0,		0,		0,		0		},	// StartElement
			{	0,		0,		1,		1,		1,		0,		0,		0,		0		},	// SurrogateCharEntity
			{	0,		0,		1,		1,		1,		0,		0,		0,		0		},	// Whitespace
			{	0,		0,		1,		1,		1,		0,		0,		0,		0		},	// EncodedContent
		};
        #endregion
    }
}
