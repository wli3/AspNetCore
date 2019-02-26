// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Microsoft.AspNetCore.Mvc.Formatters
{
    /// <summary>
    /// A <see cref="TextInputFormatter"/> for JSON content.
    /// </summary>
    public sealed class JsonInputFormatter : TextInputFormatter, IInputFormatterExceptionPolicy
    {
        private readonly JsonSerializerOptions _serializerOptions;

        /// <summary>
        /// Initializes a new instance of <see cref="JsonInputFormatter"/>.
        /// </summary>
        /// <param name="options">The <see cref="JsonFormatterOptions"/>.</param>
        public JsonInputFormatter(JsonFormatterOptions options)
        {
            _serializerOptions = options.SerializerOptions;

            SupportedEncodings.Add(UTF8EncodingWithoutBOM);
            SupportedEncodings.Add(UTF16EncodingLittleEndian);

            SupportedMediaTypes.Add(MediaTypeHeaderValues.ApplicationJson);
            SupportedMediaTypes.Add(MediaTypeHeaderValues.TextJson);
            SupportedMediaTypes.Add(MediaTypeHeaderValues.ApplicationAnyJsonSyntax);
        }

        /// <inheritdoc />
        InputFormatterExceptionPolicy IInputFormatterExceptionPolicy.ExceptionPolicy => InputFormatterExceptionPolicy.MalformedInputExceptions;

        /// <inheritdoc />
        public override async Task<InputFormatterResult> ReadRequestBodyAsync(
            InputFormatterContext context,
            Encoding encoding)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (encoding == null)
            {
                throw new ArgumentNullException(nameof(encoding));
            }

            var httpContext = context.HttpContext;
            var bodyPipe = await GetBodyPipe(httpContext, encoding);

            try
            {
                var result = await JsonSerializer.ReadAsync(bodyPipe, context.ModelType, _serializerOptions, httpContext.RequestAborted);
                return InputFormatterResult.Success(result);
            }
            catch (JsonReaderException exception)
            {
                var inputException = new InputFormatterException(exception.Message, exception);
                context.ModelState.TryAddModelException(key: string.Empty, inputException);
            }

            return InputFormatterResult.Failure();
        }

        private async Task<PipeReader> GetBodyPipe(HttpContext httpContext, Encoding encoding)
        {
            if (encoding == UTF8EncodingWithoutBOM)
            {
                return httpContext.Request.BodyPipe;
            }

            var request = httpContext.Request;
            if (!request.Body.CanSeek)
            {
                request.EnableBuffering();

                await request.Body.DrainAsync(httpContext.RequestAborted);
            }

            using (var reader = new StreamReader(request.Body))
            {
                var content = await reader.ReadToEndAsync();
                var contentBytes = encoding.GetBytes(content);

                var pipe = new Pipe();
                await pipe.Writer.WriteAsync(contentBytes.AsMemory(), httpContext.RequestAborted);

                return pipe.Reader;
            }
        }
    }
}
