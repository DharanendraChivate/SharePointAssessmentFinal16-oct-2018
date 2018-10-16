using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharePointCSOMAssessment
{
    class ApplicationCustomException : ApplicationException
    {
        public string Message
        {
            get;
        }

        public ApplicationCustomException(string ExceptionMessage)
        {
           this.Message = ExceptionMessage;
        }
    }
}
