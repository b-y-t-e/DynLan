using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace DynLan.OnpEngine.Logic
{
    public enum TokenType
    {
        OPERATOR,
        //LOGICAL_OPERATOR,
        EQUAL_OPERATOR,
        BRACKET_BEGIN,
        BRACKET_END,
        INDEXER_BEGIN,
        INDEXER_END,
        VARIABLE,
        VALUE,
        PROPERTY_NAME,
        //PROPERTY_PATH,
        WHITESPACE,
        SEPARATOR,

        //FUNCTION_CALL
    }
}