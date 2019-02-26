// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Mvc.Formatters
{
    /// <summary>
    /// A <see cref="TextOutputFormatter"/> for JSON content.
    /// </summary>
    public sealed class JsonOutputFormatter : TextOutputFormatter
    {
        private readonly JsonSerializerOptions _serializerOptions;

        /// <summary>
        /// Initializes a new <see cref="JsonOutputFormatter"/> instance.
        /// </summary>
        /// <param name="options">The <see cref="JsonFormatterOptions"/>.</param>
        public JsonOutputFormatter(JsonFormatterOptions options)
        {
            _serializerOptions = options?.SerializerOptions ?? throw new ArgumentNullException(nameof(options));

            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
            SupportedMediaTypes.Add(MediaTypeHeaderValues.ApplicationJson);
            SupportedMediaTypes.Add(MediaTypeHeaderValues.TextJson);
            SupportedMediaTypes.Add(MediaTypeHeaderValues.ApplicationAnyJsonSyntax);
        }

        /// <inheritdoc />
        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (selectedEncoding == null)
            {
                throw new ArgumentNullException(nameof(selectedEncoding));
            }

            var response = context.HttpContext.Response;
            var requestAborted = context.HttpContext.RequestAborted;

            var originalBodyPipe = response.BodyPipe;
            var pipeWriter = originalBodyPipe;

            Pipe pipe = null;
            if (selectedEncoding != Encoding.UTF8)
            {
                pipe = new Pipe();
                response.BodyPipe = pipe.Writer;
                pipeWriter = pipe.Writer;
            }

            await JsonSerializer.WriteAsync(context.Object, context.ObjectType, pipeWriter, _serializerOptions, requestAborted);
            if (pipe != null)
            {
                byte[] contentBytes;
                using (var reader = new StreamReader(response.Body))
                {
                    var content = await reader.ReadToEndAsync();
                    contentBytes = selectedEncoding.GetBytes(content);
                }

                response.BodyPipe = originalBodyPipe;
                await response.BodyPipe.WriteAsync(contentBytes.AsMemory(), requestAborted);
            }
        }
    }
}
