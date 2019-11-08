using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerce.Sales
{
    public static class HttpContextExtensions
    {
        public const string KEY_CUSTOMER_ID = "CustomerId";

        public static int UserCustomerId (this HttpContext context)
        {
            var headers = context.Request.Headers;
            if (!headers.ContainsKey(KEY_CUSTOMER_ID))
                return 0;

            if (!int.TryParse(headers[KEY_CUSTOMER_ID], out var ret))
                throw new InvalidOperationException($"customer id parse failed");

            return ret;
        }



    
    }
}
