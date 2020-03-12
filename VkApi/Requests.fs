﻿namespace VkApi.Core


[<RequireQualifiedAccess>]
module internal Requests =

    open System
    open System.Text
    open System.IO
    open System.Net
    open FSharp.Control.Tasks.V2
    open VkApi.Core.Extensions


    let AsyncGet (url: string) =
        task {
            let httpRequest = WebRequest.CreateHttp url
            return! httpRequest.AsyncGetResponse ()
        }

    let AsyncPost (url: string) filePath =
        let boundary = Guid.NewGuid () |> string

        let RequestBody filePath =
            let ContentType =
                let (|FileExtension|) (filePath: string) = Path.GetExtension filePath

                function
                | FileExtension ".txt" -> "text/plain"
                | FileExtension ".gif" -> "image/gif"
                | FileExtension ".jpeg" | FileExtension ".jpg" -> "image/jpeg"
                | FileExtension ".png" -> "image/png"
                | FileExtension ".pdf" -> "image/pdf"
                | _ -> "application/octet-stream"

            let Content =
                task {
                    use stream = File.OpenRead filePath
                    let buffer = stream.Length |> int |> Array.zeroCreate<byte>
                    let! _ = stream.ReadAsync (buffer, 0, buffer.Length)

                    return buffer
                }

            let header = sprintf "--%s\r\nContent-Disposition: form-data; name=file; filename=\"%s\"\r\nContent-Type:%s\r\n\r\n" boundary (Path.GetFileName filePath) (ContentType filePath)
                            |> Encoding.UTF8.GetBytes

            let footer = sprintf "\r\n--%s--\r\n" boundary
                            |> Encoding.UTF8.GetBytes

            task {
                let! fileContent = Content
                let buffer = Array.zeroCreate<byte> (header.Length + footer.Length + fileContent.Length)
                use stream = new MemoryStream (buffer)
                do! stream.WriteAsync (header, 0, header.Length)
                do! stream.WriteAsync (fileContent, 0, fileContent.Length)
                do! stream.WriteAsync (footer, 0, footer.Length)

                return buffer
            }

        task {
            let httpRequest = WebRequest.CreateHttp url
            httpRequest.Method <- "POST"
            httpRequest.ContentType <- sprintf "multipart/form-data; boundary=%s" boundary
            use stream = httpRequest.GetRequestStream ()
            let! body = RequestBody filePath
            do! stream.WriteAsync (body, 0, body.Length)

            return! httpRequest.AsyncGetResponse ()
        }
