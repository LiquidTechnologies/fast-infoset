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
	/// Represents a reader that provides fast, non-cached, forward-only access to XML binary encoded data that conforms to the ITU-T: Fast infoset X.891 (05/2005) recommendation.
	/// </summary>
	/// <remarks>The FIReader class derives from the System.Xml.XmlReader .Net Framework class and supports the abstract methods required to integrate with other .Net Framework XML classes such as System.Xml.XmlDocument.</remarks>
	/// <seealso cref="FIWriter"/>
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
	public sealed class FIReader : XmlReader
	{
		#region Constructors
		/// <summary>
		/// Initializes a new instance of the XmlTextReader class with the specified file.
		/// </summary>
		/// <param name="url">The URL for the file containing the XML data.</param>
		/// <exception cref="ArgumentNullException">url is a null reference (Nothing in Visual Basic)</exception>
		/// <exception cref="ArgumentException">url is an empty string.</exception>
		/// <exception cref="UriFormatException">url is not a valid URI.</exception>
		/// <exception cref="Exception">There is a runtime error (for example, an interrupted server connection).</exception>
		public FIReader(string url) : this(url, null, null) { }

		/// <summary>
		/// Initializes a new instance of the XmlTextReader class with the specified file.
		/// </summary>
		/// <param name="url">The URL for the file containing the XML data.</param>
		/// <param name="vocabularyManager">A <see cref="FIVocabularyManager"/> containing a mapping of URIs to <see cref="FIExternalVocabulary"/> objects. If the Fast Infoset to be read references an External Vocabulary, the URI used must exist with the provided vocabulary manager.</param>
		/// <exception cref="ArgumentNullException">url is a null reference (Nothing in Visual Basic)</exception>
		/// <exception cref="ArgumentException">url is an empty string.</exception>
		/// <exception cref="UriFormatException">url is not a valid URI.</exception>
		/// <exception cref="Exception">There is a runtime error (for example, an interrupted server connection).</exception>
		/// <seealso cref="FIVocabularyManager"/>
		/// <seealso cref="FIExternalVocabulary"/>
		public FIReader(string url, FIVocabularyManager vocabularyManager) : this(url, vocabularyManager, null) { }

		/// <summary>
		/// Initializes a new instance of the XmlTextReader class with the specified file.
		/// </summary>
		/// <param name="url">The URL for the file containing the XML data.</param>
		/// <param name="vocabularyManager">A <see cref="FIVocabularyManager"/> containing a mapping of URIs to <see cref="FIExternalVocabulary"/> objects. If the Fast Infoset to be read references an External Vocabulary, the URI used must exist with the provided vocabulary manager.</param>
		/// <param name="nameTable">An <see cref="XmlNameTable"/> to write strings into.</param>
		/// <exception cref="ArgumentNullException">url is a null reference (Nothing in Visual Basic)</exception>
		/// <exception cref="ArgumentException">url is an empty string.</exception>
		/// <exception cref="UriFormatException">url is not a valid URI.</exception>
		/// <exception cref="Exception">There is a runtime error (for example, an interrupted server connection).</exception>
		/// <seealso cref="FIVocabularyManager"/>
		/// <seealso cref="FIExternalVocabulary"/>
		public FIReader(string url, FIVocabularyManager vocabularyManager, XmlNameTable nameTable)
		{
			if (url == null)
				throw new ArgumentNullException("url is null.");

			if (url.Length == 0)
                throw new ArgumentException("url is an empty string.");

			Stream input = (Stream)((new XmlUrlResolver()).GetEntity(new Uri(url), null, typeof(Stream)));

			Init(input, vocabularyManager, nameTable);
		}

		/// <summary>
		/// Initializes a new instance of the XmlTextReader class with the specified stream.
		/// </summary>
		/// <param name="input">The stream containing the XML data to read.</param>
		/// <exception cref="ArgumentNullException">input is a null reference (Nothing in Visual Basic).</exception>
		public FIReader(Stream input) : this(input, null, null) { }

		/// <summary>
		/// Initializes a new instance of the XmlTextReader class with the specified stream.
		/// </summary>
		/// <param name="input">The stream containing the XML data to read.</param>
		/// <param name="vocabularyManager">A <see cref="FIVocabularyManager"/> containing a mapping of URIs to <see cref="FIExternalVocabulary"/> objects. If the Fast Infoset to be read references an External Vocabulary, the URI used must exist with the provided vocabulary manager.</param>
		/// <exception cref="ArgumentNullException">input is a null reference (Nothing in Visual Basic).</exception>
		/// <seealso cref="FIVocabularyManager"/>
		/// <seealso cref="FIExternalVocabulary"/>
		public FIReader(Stream input, FIVocabularyManager vocabularyManager) : this(input, vocabularyManager, null) { }

		/// <summary>
		/// Initializes a new instance of the XmlTextReader class with the specified stream.
		/// </summary>
		/// <param name="input">The stream containing the XML data to read.</param>
		/// <param name="vocabularyManager">A <see cref="FIVocabularyManager"/> containing a mapping of URIs to <see cref="FIExternalVocabulary"/> objects. If the Fast Infoset to be read references an External Vocabulary, the URI used must exist with the provided vocabulary manager.</param>
		/// <param name="nameTable">An <see cref="XmlNameTable"/> to write strings into.</param>
		/// <exception cref="ArgumentNullException">input is a null reference (Nothing in Visual Basic).</exception>
		/// <seealso cref="FIVocabularyManager"/>
		/// <seealso cref="FIExternalVocabulary"/>
		public FIReader(Stream input, FIVocabularyManager vocabularyManager, XmlNameTable nameTable)
		{
			Init(input, vocabularyManager, nameTable);
		}

		private void Init(Stream input, FIVocabularyManager vocabularyManager, XmlNameTable nameTable)
		{
			if (input == null)
				throw new ArgumentNullException("Stream is null.");

			_state = ReadState.Initial;
			_nodeType = XmlNodeType.None;
			_currentNode = null;
			_currentAttributeIndex = -1;
			_currentAttributeValue = null;
			_attributeValueIndex = -1;

			_parser = new FIParser(input, vocabularyManager, nameTable);
		}
		#endregion

		#region XmlReader Abstract Overrides
		/// <summary>
		/// Gets the number of attributes on the current node.
		/// </summary>
		/// <value>The number of attributes on the current node.</value>
		/// <remarks>This property is relevant to Element, DocumentType and XmlDeclaration nodes only. (Other node types do not have attributes.)</remarks>
		public override int AttributeCount
		{
			get
			{
				int count = 0;

				if (_currentNode != null)
					count = _currentNode.AttributeCount;

				return count;
			}
		}

		/// <summary>
		/// The method or operation is not implemented.
		/// </summary>
		/// <value>An empty string.</value>
		public override string BaseURI { get { return string.Empty; } }

		/// <summary>
		/// Changes the <see cref="ReadState"/> to Closed.
		/// </summary>
		/// <remarks><para>This method also releases any resources held while reading. If this reader was constructed using a stream, this method also calls Close on the underlying stream.</para>If Close has already been called, no action is performed.</remarks>
		public override void Close()
		{
			if (_state != ReadState.Closed)
			{
				_parser.Close();
				_state = ReadState.Closed;
				_currentNode = null;
				_currentAttributeIndex = -1;
				_currentAttributeValue = null;
			}
		}

		/// <summary>
		/// Gets the depth of the current node in the XML document.
		/// </summary>
		/// <value>The depth of the current node in the XML document.</value>
		public override int Depth
		{
			get
			{
				int depth = _currentNode.Depth;

				if (_nodeType == XmlNodeType.Attribute)
					// attribute name depth is 1 more than current node
					depth++;
				else if (_attributeValueIndex != -1)
					// attribute value depth is 1 more than attribute name
					depth = depth + 2;

				return depth;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the reader is positioned at the end of the stream.
		/// </summary>
		/// <value>true if the reader is positioned at the end of the stream; otherwise, false.</value>
		public override bool EOF
		{
			get { return (ReadState == ReadState.EndOfFile); }
		}

		/// <summary>
		/// Gets the value of the attribute with the specified index.
		/// </summary>
		/// <param name="i">The index of the attribute. The index is zero-based. (The first attribute has index 0.)</param>
		/// <returns>The value of the specified attribute.</returns>
		/// <remarks>This method does not move the reader.</remarks>
		public override string GetAttribute(int i)
		{
			// Do not move current node poition
			
			string val = null;

			if (_currentNode != null)
			{
				if ((i < 0) || (i >= _currentNode.AttributeCount))
					throw new ArgumentOutOfRangeException("i");

				val = _currentNode.Attributes[i].value;
			}

			// return null if not found
			return val;
		}

		/// <summary>
		/// Gets the value of the attribute with the specified local name and namespace URI.
		/// </summary>
		/// <param name="name">The local name of the attribute.</param>
		/// <param name="namespaceURI">The namespace URI of the attribute.</param>
		/// <returns>The value of the specified attribute. If the attribute is not found, a null reference (Nothing in Visual Basic) is returned. This method does not move the reader.</returns>
		/// <remarks>This method does not move the reader.</remarks>
		public override string GetAttribute(string name, string namespaceURI)
		{
			// Do not move current node poition

			string val = null;

			if (_currentNode != null)
			{
				for (int n = 0; n < _currentNode.AttributeCount; n++)
				{
					FIParser.FINode.QNameValue qnameValue = _currentNode.Attributes[n];
					if ((qnameValue.qname.localName == name) && (qnameValue.qname.ns == namespaceURI))
					{
						val = qnameValue.value;
						break;
					}
				}
			}

			// return null if not found
			return val;
		}

		/// <summary>
		/// Gets the value of the attribute with the specified name.
		/// </summary>
		/// <param name="name">The qualified name of the attribute.</param>
		/// <returns>The value of the specified attribute. If the attribute is not found, a null reference (Nothing in Visual Basic) is returned.</returns>
		/// <remarks>This method does not move the reader.</remarks>
		public override string GetAttribute(string name)
		{
			// Do not move current node poition

			string val = null;

			if (_currentNode != null)
			{
				for (int n = 0; n < _currentNode.AttributeCount; n++)
				{
					FIParser.FINode.QNameValue qnameValue = _currentNode.Attributes[n];
					if (qnameValue.qname.localName == name)
					{
						val = qnameValue.value;
						break;
					}
				}
			}

			// return null if not found
			return val;
		}

		/// <summary>
		/// Gets a value indicating whether the current node can have a <see cref="Value"/> other than String.Empty. 
		/// </summary>
		/// <value>true if the node on which the reader is currently positioned can have a Value; otherwise, false.</value>
		/// <remarks>This property is relevant to Attribute, CDATA, Comment, ProcessingInstruction, Text and XmlDeclaration</remarks>
		public override bool HasValue
		{
			get
			{
				return ((_nodeType == XmlNodeType.Attribute)
						|| (_nodeType == XmlNodeType.CDATA)
						|| (_nodeType == XmlNodeType.Comment)
						|| (_nodeType == XmlNodeType.DocumentType)
						|| (_nodeType == XmlNodeType.ProcessingInstruction)
						|| (_nodeType == XmlNodeType.SignificantWhitespace)
						|| (_nodeType == XmlNodeType.Text)
						|| (_nodeType == XmlNodeType.Whitespace)
						|| (_nodeType == XmlNodeType.XmlDeclaration));
			}
		}

		/// <summary>
		/// The method or operation is not implemented.
		/// </summary>
		/// <value>false</value>
		public override bool IsEmptyElement
		{
			get { return false; }
		}

		/// <summary>
		/// Gets the local name of the current node.
		/// </summary>
		/// <value>The name of the current node with the prefix removed. For example, LocalName is book for the element &lt;bk:book&gt;. For node types that do not have a name (like Text, Comment, and so on), this property returns String.Empty.</value>
		public override string LocalName
		{
			get
			{
				string val = null;

				if ((_nodeType == XmlNodeType.Attribute) || (_attributeValueIndex != -1))
					val = _currentNode.Attributes[_currentAttributeIndex].qname.localName;
				else if (_currentNode != null)
					val = _currentNode.QName.localName;

				// return String.Empty if not found
				return ((val == null) ? String.Empty : val);
			}
		}

		/// <summary>
		/// The method or operation is not implemented.
		/// </summary>
		/// <param name="prefix">The prefix whose namespace URI you want to resolve. To match the default namespace, pass an empty string.</param>
		/// <returns>The namespace URI to which the prefix maps or a null reference (Nothing in Visual Basic) if no matching prefix is found.</returns>
		/// <exception cref="ArgumentNullException">prefix value is a null reference (Nothing in Visual Basic).</exception>
		/// <exception cref="Exception">The method or operation is not implemented.</exception>
		public override string LookupNamespace(string prefix)
		{
			if (prefix == null)
				throw new ArgumentNullException("prefix");

			throw new Exception("The method or operation is not implemented.");
		}

		/// <summary>
		/// Moves to the attribute with the specified local name and namespace URI.
		/// </summary>
		/// <param name="name">The local name of the attribute.</param>
		/// <param name="ns">The namespace URI of the attribute.</param>
		/// <returns>true if the attribute is found; otherwise, false. If false, the reader's position does not change.</returns>
		/// <remarks>After calling MoveToAttribute, the <see cref="LocalName"/>, <see cref="NamespaceURI"/>, and <see cref="Prefix"/> properties reflects the properties of that attribute.</remarks>
		public override bool MoveToAttribute(string name, string ns)
		{
			bool val = false;

			if (_currentNode != null)
			{
				int index = 0;
				for (int n = 0; n < _currentNode.AttributeCount; n++)
				{
					FIParser.FINode.QNameValue qnameValue = _currentNode.Attributes[n];
					if ((qnameValue.qname.localName == name) && (qnameValue.qname.ns == ns))
					{
						_nodeType = XmlNodeType.Attribute;
						_currentAttributeIndex = index;
						_currentAttributeValue = null;
						_attributeValueIndex = -1;
						val = true;
						break;
					}

					index++;
				}
			}

			return val;
		}

		/// <summary>
		/// Moves to the attribute with the specified name.
		/// </summary>
		/// <param name="name">The qualified name of the attribute. </param>
		/// <returns>true if the attribute is found; otherwise, false. If false, the reader's position does not change.</returns>
		/// <remarks>After calling MoveToAttribute, the <see cref="LocalName"/>, <see cref="NamespaceURI"/>, and <see cref="Prefix"/> properties reflects the properties of that attribute.</remarks>
		public override bool MoveToAttribute(string name)
		{
			bool val = false;

			if (_currentNode != null)
			{
				int index = 0;
				for (int n = 0; n < _currentNode.AttributeCount; n++)
				{
					FIParser.FINode.QNameValue qnameValue = _currentNode.Attributes[n];
					if (qnameValue.qname.localName == name)
					{
						_nodeType = XmlNodeType.Attribute;
						_currentAttributeIndex = index;
						_currentAttributeValue = null;
						_attributeValueIndex = -1;
						val = true;
						break;
					}
					index++;
				}
			}

			return val;
		}

		/// <summary>
		/// Moves to the attribute with the specified index.
		/// </summary>
		/// <param name="i">The index of the attribute.</param>
		/// <remarks>After calling MoveToAttribute, the <see cref="LocalName"/>, <see cref="NamespaceURI"/>, and <see cref="Prefix"/> properties reflects the properties of that attribute.</remarks>
		/// <exception cref="ArgumentOutOfRangeException">The i parameter is less than 0 or greater than or equal to AttributeCount.</exception>
		public override void MoveToAttribute(int i)
		{
			if (_currentNode != null)
			{
				if ((i < 0) || (i >= _currentNode.AttributeCount))
					throw new ArgumentOutOfRangeException("i");

				_nodeType = XmlNodeType.Attribute;
				_currentAttributeIndex = i;
				_currentAttributeValue = null;
				_attributeValueIndex = -1;
			}
		}

		/// <summary>
		/// Moves to the element that contains the current attribute node.
		/// </summary>
		/// <returns>true if the reader is positioned on an attribute (the reader moves to the element that owns the attribute); false if the reader is not positioned on an attribute (the position of the reader does not change).</returns>
		/// <remarks>Use this method to return to an element after navigating through its attributes. This method moves the reader to one of the following node types: Element, DocumentType, or XmlDeclaration.</remarks>
		public override bool MoveToElement()
		{
			// Moves to the element that contains the current attribute node.
			bool val = false;

			if ((_nodeType == XmlNodeType.Attribute) || (_attributeValueIndex != -1))
			{
				_nodeType = _currentNode.NodeType;
				_currentAttributeIndex = -1;
				_currentAttributeValue = null;
				_attributeValueIndex = -1;
				val = true;
			}

			return val;
		}

		/// <summary>
		/// Moves to the first attribute.
		/// </summary>
		/// <returns>true if an attribute exists (the reader moves to the first attribute); otherwise, false (the position of the reader does not change).</returns>
		public override bool MoveToFirstAttribute()
		{
			_currentAttributeIndex = -1;
			_attributeValueIndex = -1;
			return MoveToNextAttribute();
		}

		/// <summary>
		/// Moves to the next attribute.
		/// </summary>
		/// <returns>true if there is a next attribute; false if there are no more attributes.</returns>
		/// <remarks>If the current node is an element node, this method is equivalent to <see cref="MoveToFirstAttribute"/>. If MoveToNextAttribute returns true, the reader moves to the next attribute; otherwise, the position of the reader does not change.</remarks>
		public override bool MoveToNextAttribute()
		{
			bool val = false;

			if ((_currentNode != null)
				&& (_currentNode.AttributeCount > (_currentAttributeIndex + 1)))
			{
				_nodeType = XmlNodeType.Attribute;
				_currentAttributeIndex++;
				_currentAttributeValue = null;
				_attributeValueIndex = -1;
				val = true;
			}

			return val;
		}

		/// <summary>
		/// The <see cref="XmlNameTable"/> associated with this implementation
		/// </summary>
		/// <exception cref="Exception">The method or operation is not implemented.</exception>
		public override XmlNameTable NameTable
		{
			get { return _parser.NameTable; }
		}

		/// <summary>
		/// Gets the namespace URI (as defined in the W3C Namespace specification) of the node on which the reader is positioned.
		/// </summary>
		/// <value>The namespace URI of the current node; otherwise an empty string.</value>
		public override string NamespaceURI
		{
			get
			{
				string val = null;

				if ((_nodeType == XmlNodeType.Attribute) || (_attributeValueIndex != -1))
					val = _currentNode.Attributes[_currentAttributeIndex].qname.ns;
				else if (_currentNode != null)
					val = _currentNode.QName.ns;

				// return String.Empty if not found
				return ((val == null) ? String.Empty : val);
			}
		}


		/// <summary>
		/// Gets the type of the current node.
		/// </summary>
		/// <value>One of the <see cref="XmlNodeType"/> values representing the type of the current node.</value>
		/// <remarks>This property never returns the following XmlNodeType types: Document, DocumentFragment, Entity, EndEntity, or Notation.</remarks>
		public override XmlNodeType NodeType
		{
			get { return _nodeType; }
		}

		/// <summary>
		/// Gets the namespace prefix associated with the current node.
		/// </summary>
		/// <value>The namespace prefix associated with the current node.</value>
		public override string Prefix
		{
			get
			{
				string val = null;

				if ((_nodeType == XmlNodeType.Attribute) || (_attributeValueIndex != -1))
					val = _currentNode.Attributes[_currentAttributeIndex].qname.prefix;
				else if (_currentNode != null)
					val = _currentNode.QName.prefix;

				// return String.Empty if not found
				return ((val == null) ? String.Empty : val);
			}
		}

		/// <summary>
		/// Reads the next node from the stream.
		/// </summary>
		/// <returns>true if the next node was read successfully; false if there are no more nodes to read.</returns>
		/// <exception cref="LtFastInfosetException">An error occurred while parsing the Fast Infoset.</exception>
		/// <remarks>When a reader is first created and initialized, there is no information available. You must call Read to read the first node.</remarks>
		public override bool Read()
		{
			try
			{
				_currentAttributeIndex = -1;
				_currentAttributeValue = null;
				_attributeValueIndex = -1;
				_currentNode = _parser.Read();

				if (_currentNode == null)
				{
					_nodeType = XmlNodeType.None;
					_state = ReadState.EndOfFile;
				}
				else
				{
					_nodeType = _currentNode.NodeType;
					_state = ReadState.Interactive;
				}

				return (_currentNode != null);
			}
			catch (LtFastInfosetException ex)
			{
				_state = ReadState.Error;
				throw ex;
			}
		}

		/// <summary>
		/// Parses the attribute value into one or more Text, EntityReference, or EndEntity nodes.
		/// </summary>
		/// <returns>true if there are nodes to return. false if the reader is not positioned on an attribute node when the initial call is made or if all the attribute values have been read. An empty attribute, such as, misc="", returns true with a single node with a value of String.Empty.</returns>
		/// <remarks>Use this method after calling MoveToAttribute to read through the text or entity reference nodes that make up the attribute value. The Depth of the attribute value nodes is one plus the <see cref="Depth"/> of the attribute node; it increments and decrements by one when you step into and out of general entity references.</remarks>
		public override bool ReadAttributeValue()
		{
			bool val = false;

			// TODO: Add support for parsing attribute value into one or more Text, EntityReference, or EndEntity nodes
			if (_nodeType == XmlNodeType.Attribute)
			{
				if (_attributeValueIndex == -1)
				{
					_nodeType = XmlNodeType.Text;
					_currentAttributeValue = _currentNode.Attributes[_currentAttributeIndex].value;
					_attributeValueIndex++;
					val = true;
				}
				else
				{
					// TODO: See if we have more values to return
					_currentAttributeValue = null;
					_attributeValueIndex = -1;
				}
			}

			return val;
		}

		/// <summary>
		/// Gets the state of the reader.
		/// </summary>
		/// <value>One of the ReadState values.</value>
		public override ReadState ReadState
		{
			get { return _state; }
		}

		/// <summary>
		/// The method or operation is not implemented.
		/// </summary>
		/// <exception cref="Exception">The method or operation is not implemented.</exception>
		public override void ResolveEntity()
		{
			throw new Exception("The method or operation is not implemented.");
		}

		/// <summary>
		/// Gets the text value of the current node.
		/// </summary>
		/// <value>The value returned depends on the <see cref="NodeType"/> of the node. The following table lists node types that have a value to return. All other node types return String.Empty.
		/// <para>Attribute - The value of the attribute.</para>
		/// <para>CDATA - The content of the CDATA section.</para>
		/// <para>Comment - The content of the comment.</para>
		/// <para>DocumentType - The internal subset.</para>
		/// <para>ProcessingInstruction - The entire content, excluding the target.</para>
		/// <para>SignificantWhitespace - The white space within an xml:space= 'preserve' scope.</para>
		/// <para>Text - The content of the text node.</para>
		/// <para>Whitespace - The white space between markup.</para>
		/// <para>XmlDeclaration - The content of the declaration.</para></value>
		public override string Value
		{
			get
			{
				string val = null;

				if (_nodeType == XmlNodeType.Attribute)
					val = _currentNode.Attributes[_currentAttributeIndex].value;
				else if (_attributeValueIndex != -1)
					val = _currentAttributeValue;
				else if (_currentNode != null)
					val = _currentNode.Value;

				// return String.Empty if not found
				return ((val == null) ? String.Empty : val);
			}
		}
		#endregion

		#region Member Variables
		private ReadState _state;
		private FIParser _parser;

		private FIParser.FINode _currentNode;
		private int _currentAttributeIndex;
		private XmlNodeType _nodeType;
		private string _currentAttributeValue;
		private int _attributeValueIndex;
		#endregion
	}
}
