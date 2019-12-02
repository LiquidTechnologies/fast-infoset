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
using System.Text;

namespace LiquidTechnologies.FastInfoset
{
	internal class FIReaderVocabulary
	{
		#region Constructors
		internal FIReaderVocabulary()
		{
			// internal vocabulary constructor
			_encodingAlgorithmManager = new FIEncodingAlgorithmManager();
			_restrictedAlphabetManager = new FIRestrictedAlphabetManager();

			Init();
		}

		internal FIReaderVocabulary(FIEncodingAlgorithmManager encodingAlgorithmManager, FIRestrictedAlphabetManager restrictedAlphabetManager)
		{
			// external vocabulary constructor
			_encodingAlgorithmManager = encodingAlgorithmManager;
			_restrictedAlphabetManager = restrictedAlphabetManager;

			Init();
		}

		internal FIReaderVocabulary(FIReaderVocabulary vocab)
		{
			// copy constructor
			_encodingAlgorithmManager = vocab._encodingAlgorithmManager;
			_restrictedAlphabetManager = vocab._restrictedAlphabetManager;

			_attributeNames = new List<QualifiedName>(vocab._attributeNames);
			_attributeValues = new List<string>(vocab._attributeValues);
			_elementNames = new List<QualifiedName>(vocab._elementNames);
			_contentCharacterChunks = new List<string>(vocab._contentCharacterChunks);
			_localNames = new List<string>(vocab._localNames);
			_namespaceNames = new List<string>(vocab._namespaceNames);
			_prefixNames = new List<string>(vocab._prefixNames);
			_otherNCNames = new List<string>(vocab._otherNCNames);
			_otherStrings = new List<string>(vocab._otherStrings);
		}

		private void Init()
		{
			_attributeNames = new List<QualifiedName>();
			_attributeValues = new List<string>();
			_elementNames = new List<QualifiedName>();
			_contentCharacterChunks = new List<string>();
			_localNames = new List<string>();
			_namespaceNames = new List<string>();
			_prefixNames = new List<string>();
			_otherNCNames = new List<string>();
			_otherStrings = new List<string>();

			// add default prefix and namespace
			_prefixNames.Add(FIConsts.FI_DEFAULT_PREFIX);
			_namespaceNames.Add(FIConsts.FI_DEFAULT_NAMESPACE);
		}
		#endregion

		#region Internal Interface
		#region Add Methods
		internal void AddRestrictedAlphabet(string alphabetChars)
		{
			_restrictedAlphabetManager.Add(new FIRestrictedAlphabet(alphabetChars));
		}
		
		internal void AddAttribute(string prefix, string ns, string localName)
		{
			QualifiedName qname = new QualifiedName();
			qname.Init(prefix, ns, localName);
			AddAttribute(qname);
		}

		internal void AddElement(string prefix, string ns, string localName)
		{
			QualifiedName qname = new QualifiedName();
			qname.Init(prefix, ns, localName);
			AddElement(qname);
		}

		internal void AddAttribute(QualifiedName qname)
		{
			if (_attributeNames.Count == FIConsts.TWO_POWER_TWENTY)
				throw new LtFastInfosetException("Table is Full.");

			_attributeNames.Add(qname);
		}

		internal void AddElement(QualifiedName qname)
		{
			if (_elementNames.Count == FIConsts.TWO_POWER_TWENTY)
				throw new LtFastInfosetException("Table is Full.");

			_elementNames.Add(qname);
		}

		internal int AddAttributeValue(string value)
		{
			if (_attributeValues.Count == FIConsts.TWO_POWER_TWENTY)
				throw new LtFastInfosetException("Too many items in index.");

			_attributeValues.Add(value);
			return _attributeValues.Count;
		}

		internal int AddContentCharacterChunk(string value)
		{
			if (_contentCharacterChunks.Count == FIConsts.TWO_POWER_TWENTY)
				throw new LtFastInfosetException("Too many items in index.");

			_contentCharacterChunks.Add(value);
			return _contentCharacterChunks.Count;
		}

		internal int AddPrefixName(string prefix)
		{
			if (_prefixNames.Count == FIConsts.TWO_POWER_TWENTY)
				throw new LtFastInfosetException("Too many items in index.");

			_prefixNames.Add(prefix);
			return _prefixNames.Count;
		}

		internal int AddNamespaceName(string ns)
		{
			if (_namespaceNames.Count == FIConsts.TWO_POWER_TWENTY)
				throw new LtFastInfosetException("Too many items in index.");

			_namespaceNames.Add(ns);
			return _namespaceNames.Count;
		}

		internal int AddLocalName(string localName)
		{
			if (_localNames.Count == FIConsts.TWO_POWER_TWENTY)
				throw new LtFastInfosetException("Too many items in index.");

			_localNames.Add(localName);
			return _localNames.Count;
		}

		internal int AddOtherNCName(string otherNCName)
		{
			if (_otherNCNames.Count == FIConsts.TWO_POWER_TWENTY)
				throw new LtFastInfosetException("Too many items in index.");

			_otherNCNames.Add(otherNCName);
			return _otherNCNames.Count;
		}

		internal int AddOtherString(string otherString)
		{
			if (_otherStrings.Count == FIConsts.TWO_POWER_TWENTY)
				throw new LtFastInfosetException("Too many items in index.");

			_otherStrings.Add(otherString);
			return _otherStrings.Count;
		}

		internal void AddQNameToVector(QualifiedName qname, List<QualifiedName> vecQNames)
		{
			if (vecQNames.Count == FIConsts.TWO_POWER_TWENTY)
				vecQNames.Add(qname);
		}

		internal void AddValueToMap(string value, Dictionary<string, int> map)
		{
			if (map.Count == FIConsts.TWO_POWER_TWENTY)
				map.Add(value, map.Count + 1);
		}
		#endregion

		#region Lookup Value Methods
		internal FIRestrictedAlphabet RestrictedAlphabet(int fiTableIndex)
		{
			return _restrictedAlphabetManager.Alphabet(fiTableIndex);
		}

		internal FIEncoding EncodingAlgorithm(string uri)
		{
			return _encodingAlgorithmManager.Encoding(uri);
		}

		internal FIEncoding EncodingAlgorithm(int fiTableIndex)
		{
			return _encodingAlgorithmManager.Encoding(fiTableIndex);
		}

		internal QualifiedName AttributeName(int index)
		{
			if ((index < 0) || (index >= _attributeNames.Count))
				throw new LtFastInfosetException("Attribute Name index out of bounds.");

			return _attributeNames[index];
		}

		internal QualifiedName ElementName(int index)
		{
			if ((index < 0) || (index >= _elementNames.Count))
				throw new LtFastInfosetException("Element Name index out of bounds.");

			return _elementNames[index];
		}

		internal string AttributeValue(int index)
		{
			if ((index < 0) || (index >= _attributeValues.Count))
				throw new LtFastInfosetException("Attribute Value index out of bounds.");

			return _attributeValues[index];
		}

		internal string CharacterChunk(int index)
		{
			if ((index < 0) || (index >= _contentCharacterChunks.Count))
				throw new LtFastInfosetException("Character Chunk index out of bounds.");

			return _contentCharacterChunks[index];
		}

		internal string PrefixName(int index)
		{
			if ((index < 0) || (index >= _prefixNames.Count))
				throw new LtFastInfosetException("Prefix index out of bounds.");

			return _prefixNames[index];
		}
		
		internal string NamespaceName(int index)
		{
			if ((index < 0) || (index >= _namespaceNames.Count))
				throw new LtFastInfosetException("Namespace index out of bounds.");

			return _namespaceNames[index];
		}

		internal string LocalName(int index)
		{
			if ((index < 0) || (index >= _localNames.Count))
				throw new LtFastInfosetException("Local Name index out of bounds.");

			return _localNames[index];
		}

		internal string OtherNCName(int index)
		{
			if ((index < 0) || (index >= _otherNCNames.Count))
				throw new LtFastInfosetException("Other NC Name index out of bounds.");

			return _otherNCNames[index];
		}

		internal string OtherString(int index)
		{
			if ((index < 0) || (index >= _otherStrings.Count))
				throw new LtFastInfosetException("Other String index out of bounds.");

			return _otherStrings[index];
		}
		#endregion
		#endregion

		#region Member Variables
		private FIEncodingAlgorithmManager _encodingAlgorithmManager;
		private FIRestrictedAlphabetManager _restrictedAlphabetManager;

		private List<QualifiedName> _attributeNames;
		private List<string> _attributeValues;

		private List<QualifiedName> _elementNames;
		private List<string> _contentCharacterChunks;

		private List<string> _localNames;
		private List<string> _namespaceNames;
		private List<string> _prefixNames;

		private List<string> _otherNCNames;
		private List<string> _otherStrings;
		#endregion
	}
}
