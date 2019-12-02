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
using System.Globalization;
using System.IO;
using System.Text;

namespace LiquidTechnologies.FastInfoset
{
	class InternalFIWriter
	{
		#region Inner Classes
		private class NamespaceManager
		{
			private const int NS_GROW_SIZE = 8;
			private const int ELM_INIT_SIZE = 64; // exponential

			#region Inner Struct
			private struct NamespaceInfo
			{
				internal void Init(string prefix, string ns)
				{
					this.prefix = prefix;
					this.ns = ns;
					prevNsIndex = -1;
				}

				internal string prefix;
				internal string ns;
				internal int prevNsIndex;
			}

			private struct ElementInfo
			{
				internal void Init(int namespaceTop)
				{
					defaultNamespace = string.Empty;
					defaultNamespaceDeclared = false;
					prevNamespaceTop = namespaceTop;
					prefixCount = 0;
				}

				internal string defaultNamespace;
				internal bool defaultNamespaceDeclared;
				internal int prevNamespaceTop;
				internal int prefixCount;
			}
			#endregion

			internal NamespaceManager()
			{
				_namespaceStack = new NamespaceInfo[NS_GROW_SIZE];
				_namespaceTop = -1;

				_elementStack = new ElementInfo[ELM_INIT_SIZE];
				_elementTop = 0;
				_elementStack[_elementTop].Init(-1);
			}

			internal string DefaultNamespace
			{
				get { return _elementStack[_elementTop].defaultNamespace; }
			}

			internal string GeneratePrefix()
			{
				int num = _elementStack[_elementTop].prefixCount++ + 1;
				return ("d" + _elementTop.ToString("d", CultureInfo.InvariantCulture) + "p" + num.ToString("d", CultureInfo.InvariantCulture));
			}
			
			internal string FindPrefix(string ns)
			{
				for (int i = _namespaceTop; i >= 0; i--)
				{
					if ((_namespaceStack[i].ns == ns) && (LookupNamespace(_namespaceStack[i].prefix) == i))
						return _namespaceStack[i].prefix;
				}

				return null;
			}

			internal int LookupNamespace(string prefix)
			{
				for (int i = _namespaceTop; i >= 0; i--)
				{
					if (_namespaceStack[i].prefix == prefix)
						return i;
				}

				return -1;
			}

			internal int LookupNamespaceInCurrentScope(string prefix)
			{
				for (int i = _namespaceTop; i > _elementStack[_elementTop].prevNamespaceTop; i--)
				{
					if (_namespaceStack[i].prefix == prefix)
						return i;
				}

				return -1;
			}

			internal void PushStack(string prefix, string ns, string localName)
			{
				if (_elementTop == (_elementStack.Length - 1))
				{
					ElementInfo[] destinationArray = new ElementInfo[_elementStack.Length * 2];
					if (_elementTop > 0)
						Array.Copy(_elementStack, destinationArray, _elementTop + 1);

					_elementStack = destinationArray;
				}

				_elementTop++;
				_elementStack[_elementTop].Init(_namespaceTop);

				_elementStack[_elementTop].defaultNamespace = _elementStack[_elementTop - 1].defaultNamespace;
				_elementStack[_elementTop].defaultNamespaceDeclared = _elementStack[_elementTop - 1].defaultNamespaceDeclared;

				if (ns == null)
				{
					if (((prefix != null) && (prefix.Length != 0)) && (LookupNamespace(prefix) == -1))
						throw new LtFastInfosetException("Undefined Namespace for Prefix: " + prefix);
				}
				else if (prefix == null)
				{
					string text = FindPrefix(ns);
					if (text != null)
						prefix = text;
					else
						PushNamespace(null, ns);
				}
				else if (prefix.Length == 0)
				{
					PushNamespace(null, ns);
				}
				else
				{
					if (ns.Length == 0)
						prefix = null;

					PushNamespace(prefix, ns);
				}
			}

			internal void PushNamespace(string prefix, string ns)
			{
				if (FIConsts.FI_XML_NAMESPACE == ns)
					throw new LtFastInfosetException("Reserved Namespace: " + FIConsts.FI_XML_NAMESPACE);

				if (prefix != null)
				{
					if ((prefix.Length != 0) && (ns.Length == 0))
						throw new LtFastInfosetException("Namespace required for Prefix: " + prefix);

					int index = LookupNamespace(prefix);
					if ((index == -1) || (_namespaceStack[index].ns != ns))
						AddNamespace(prefix, ns);
				}
				else
				{
					if (!_elementStack[_elementTop].defaultNamespaceDeclared)
					{
						_elementStack[_elementTop].defaultNamespace = ns;
						_elementStack[_elementTop].defaultNamespaceDeclared = true;
					}
				}
			}

			private void AddNamespace(string prefix, string ns)
			{
				int length = ++_namespaceTop;
				if (length == _namespaceStack.Length)
				{
					NamespaceInfo[] destinationArray = new NamespaceInfo[length + NS_GROW_SIZE];
					Array.Copy(_namespaceStack, destinationArray, length);
					_namespaceStack = destinationArray;
				}

				_namespaceStack[length].Init(prefix, ns);
			}

			private int _namespaceTop;
			private NamespaceInfo[] _namespaceStack;
			private int _elementTop;
			private ElementInfo[] _elementStack;
		}
		#endregion

		#region Constructors
		internal InternalFIWriter(Stream output, FIWriterVocabulary vocabulary)
		{
			_encoder = new FIEncoder(output, vocabulary);
			_namespaceManager = new NamespaceManager();
			_element = new FIEncoder.FIElement();
			_hasElement = false;
			_hasAttribute = false;
		}
		#endregion

		#region Internal FI Methods
		internal FIWriterVocabulary Vocabulary
		{
			get { return _encoder.Vocabulary; }
		}

		internal void Close()
		{
			if (_encoder != null)
				_encoder.Close();

			_encoder = null;
			_namespaceManager = null;
		}

		internal void Flush()
		{
			if (_encoder != null)
				_encoder.Flush();
		}

		internal string LookupPrefix(string ns)
		{
			if (string.IsNullOrEmpty(ns))
				throw new LtFastInfosetException("Invalid Namespace");

			string prefix = _namespaceManager.FindPrefix(ns);

			if ((prefix == null) && (ns == _namespaceManager.DefaultNamespace))
			{
				prefix = string.Empty;
			}

			return prefix;
		}

		#region Write Document Methods
		internal void WriteStartDocument(FIWriter.FInfoDecl decl)
		{
			_encoder.WriteDeclaraion(decl);
			WriteStartDocument();
		}

		internal void WriteStartDocument()
		{
			_encoder.WriteHeader();
		}

		internal void WriteEndDocument()
		{
			_encoder.WriteEndDocument();
		}
		#endregion
		
		#region Write Element Methods
		internal void WriteStartElement(string prefix, string localName, string ns)
		{
			FlushElement();

			_element.Init(prefix, ns, localName);
			_hasElement = true;
			_namespaceManager.PushStack(prefix, ns, localName);
		}

		internal void WriteEndElement()
		{
			FlushElement();
			
			_encoder.WriteEndElement();
		}
		#endregion

		#region Write Attribute Methods
		internal void WriteStartAttribute(string prefix, string localName, string ns)
		{
			Debug.Assert(_element != null);

			_prefixForXmlNs = null;
			_isNamespaceAttribute = false;

			if ((prefix != null) && (prefix.Length == 0))
				prefix = null;

			if (((ns == FIConsts.FI_XML_NAMESPACE) && (prefix == null)) && (localName != FIConsts.FI_XML_NAMESPACE_NAME))
				prefix = FIConsts.FI_XML_NAMESPACE_NAME;

			if (prefix != FIConsts.FI_DEFAULT_PREFIX)
			{
				if (prefix == FIConsts.FI_XML_NAMESPACE_NAME)
				{
					if ((FIConsts.FI_XML_NAMESPACE != ns) && (ns != null))
						throw new LtFastInfosetException("Reserved Namespace: " + FIConsts.FI_XML_NAMESPACE);

					if ((localName == null) || (localName.Length == 0))
					{
						localName = prefix;
						prefix = null;
						_prefixForXmlNs = null;
					}
					else
						_prefixForXmlNs = localName;

					_isNamespaceAttribute = true;
				}
				else if ((prefix == null) && (localName == FIConsts.FI_XML_NAMESPACE_NAME))
				{
					if ((FIConsts.FI_XML_NAMESPACE != ns) && (ns != null))
						throw new LtFastInfosetException("Reserved Namespace: " + FIConsts.FI_XML_NAMESPACE);

					_isNamespaceAttribute = true;
					_prefixForXmlNs = null;
				}
				else if (ns == null)
				{
					if ((prefix != null) && (_namespaceManager.LookupNamespace(prefix) == -1))
						throw new LtFastInfosetException("Namespace required for Prefix: " + prefix);
				}
				else if (ns.Length == 0)
					prefix = string.Empty;
				else
				{
					if ((prefix != null) && (_namespaceManager.LookupNamespaceInCurrentScope(prefix) != -1))
						prefix = null;

					string text = _namespaceManager.FindPrefix(ns);
					if ((text != null) && ((prefix == null) || (prefix == text)))
						prefix = text;
					else
					{
						if (prefix == null)
							prefix = _namespaceManager.GeneratePrefix();

						_namespaceManager.PushNamespace(prefix, ns);
					}
				}
			}

			_attribute.Init(prefix, ns, localName);
			_hasAttribute = true;
		}

		internal void WriteEndAttribute()
		{
			Debug.Assert(_element != null);

			if (_isNamespaceAttribute)
			{
				if (_attribute.qnameIndex.qname.localName == FIConsts.FI_XML_NAMESPACE_NAME)
					_element.DefaultNamespace = (string)(_attribute.data);
				else
					_element.AddNamespaceAttribute(_attribute);
			}
			else
				_element.AddAttribute(_attribute);

			_hasAttribute = false;
		}
		#endregion

		#region Write Content Methods
		internal void WriteContent(string text)
		{
			Debug.Assert(text != null);

			if (_hasAttribute)
			{
				// save attribute value until we know if its a namespace (see WriteEndAttribute)
				if (_attribute.data == null)
					_attribute.data = text;
				else
					_attribute.data += text;

				if (_isNamespaceAttribute)
					_namespaceManager.PushNamespace(_prefixForXmlNs, text);
			}
			else
			{
				FlushElement();
				_encoder.WriteCharacterChunk(text);
			}
		}

		internal void WriteEncodedData(FIEncoding encoding, object data)
		{
			// data assumed to be of correct type for chosen encoding

			if (data == null)
				throw new LtFastInfosetException("Invalid Data");

			if (_hasAttribute)
			{
				if (_isNamespaceAttribute)
					throw new LtFastInfosetException("Namespace Attribute value cannot be encoded");

				// save attribute value until we know if its a namespace (see WriteEndAttribute)
				_attribute.encoding = encoding;
				_attribute.data = data;
			}
			else
			{
				FlushElement();
				_encoder.WriteCharacterChunk(encoding, data);
			}
		}

		internal void WriteComment(string text)
		{
			FlushElement();
			_encoder.WriteComment(text);
		}
		
		internal void WriteDocumentTypeDeclaration(string name, string pubid, string sysid, string subset)
		{
			FlushElement();
			_encoder.WriteDocumentTypeDeclaration(name, pubid, sysid, subset);
		}

		internal void WriteProcessingInstruction(string name, string text)
		{
			FlushElement();
			_encoder.WriteProcessingInstruction(name, text);
		}
		#endregion

		#endregion

		#region Private Methods
		private void FlushElement()
		{
			if (_hasElement)
			{
				_encoder.WriteElement(_element);
				_hasElement = false;
			}
		}
		#endregion

		#region Member Variables
		private FIEncoder _encoder;
		private NamespaceManager _namespaceManager;
		private FIEncoder.FIElement _element;
		private bool _hasElement;
		private FIEncoder.FIAttribute _attribute;
		private bool _hasAttribute;
		private string _prefixForXmlNs;
		private bool _isNamespaceAttribute;
		#endregion
	}
}
