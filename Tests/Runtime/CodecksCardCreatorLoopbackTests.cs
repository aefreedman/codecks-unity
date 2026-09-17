using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codecks.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Codecks.Tests.PlayMode
{
    public class CodecksCardCreatorLoopbackTests
    {
        [UnityTest]
        public IEnumerator CreateNewCard_NoAttachments_SucceedsAndSerializesFields()
        {
            using var server = new LoopbackServer(request => request.Path == "/create"
                ? Response.Json("{\"ok\":true,\"cardId\":\"card-no-files\"}")
                : Response.Status(404));

            yield return RunRequest(server, null, CodecksCardCreator.CodecksSeverity.High, "reporter@example.test",
                (success, result, callbackCount) =>
                {
                    Assert.That(success, Is.True);
                    Assert.That(result, Is.EqualTo("card-no-files"));
                    Assert.That(callbackCount, Is.EqualTo(1));
                });

            Assert.That(server.Requests.Length, Is.EqualTo(1));
            Assert.That(server.Requests[0].Body, Does.Contain("report body is long enough"));
            Assert.That(server.Requests[0].Body, Does.Contain("\"severity\":\"high\""));
            Assert.That(server.Requests[0].Body, Does.Contain("\"userEmail\":\"reporter@example.test\""));
            Assert.That(server.Requests[0].Body, Does.Contain("\"fileNames\":[]"));
        }

        [UnityTest]
        public IEnumerator CreateNewCard_AttachmentUpload_Succeeds()
        {
            LoopbackServer server = null;
            server = new LoopbackServer(request =>
            {
                if (request.Path == "/create")
                    return Response.Json("{\"ok\":true,\"cardId\":\"card-with-file\",\"uploadUrls\":[{\"fileName\":\"report.txt\",\"url\":\"" + server.Url + "upload\",\"fields\":{\"key\":\"uploads/report.txt\"}}]}");
                return request.Path == "/upload" ? Response.Status(204) : Response.Status(404);
            });
            using (server)
            {
                var files = new Dictionary<string, (byte[], CodecksCardCreator.CodecksFileType)>
                {
                    ["report.txt"] = (Encoding.UTF8.GetBytes("attachment bytes"), CodecksCardCreator.CodecksFileType.PlainText)
                };
                yield return RunRequest(server, files, CodecksCardCreator.CodecksSeverity.Low, null,
                    (success, result, callbackCount) =>
                    {
                        Assert.That(success, Is.True);
                        Assert.That(result, Is.EqualTo("card-with-file"));
                        Assert.That(callbackCount, Is.EqualTo(1));
                    });

                Assert.That(server.Requests.Length, Is.EqualTo(2));
                Assert.That(server.Requests[0].Body, Does.Contain("\"severity\":\"low\""));
                Assert.That(server.Requests[0].Body, Does.Contain("report.txt"));
                Assert.That(server.Requests[1].Body, Does.Contain("attachment bytes"));
            }
        }

        [UnityTest]
        public IEnumerator CreateNewCard_ApiRejection_CompletesOnceWithFailure()
        {
            using var server = new LoopbackServer(_ => Response.Json("{\"ok\":false}"));
            yield return AssertFailure(server, null, "Codecks rejected the report request.");
        }

        [UnityTest]
        public IEnumerator CreateNewCard_HttpError_CompletesOnceWithFailure()
        {
            using var server = new LoopbackServer(_ => Response.Status(500));
            yield return AssertFailure(server, null, "request unsuccessful: ProtocolError");
        }

        [UnityTest]
        public IEnumerator CreateNewCard_MalformedJson_CompletesOnceWithFailure()
        {
            using var server = new LoopbackServer(_ => Response.Json("not json"));
            yield return AssertFailure(server, null, "Codecks returned an invalid report response.");
        }

        [UnityTest]
        public IEnumerator CreateNewCard_MalformedUploadInstructions_CompletesOnceWithFailure()
        {
            using var server = new LoopbackServer(_ => Response.Json("{\"ok\":true,\"cardId\":\"card\",\"uploadUrls\":[{\"fileName\":\"report.txt\",\"url\":\"not-a-url\",\"fields\":{}}]}"));
            var files = new Dictionary<string, (byte[], CodecksCardCreator.CodecksFileType)>
            {
                ["report.txt"] = (new byte[] { 1 }, CodecksCardCreator.CodecksFileType.PlainText)
            };
            yield return AssertFailure(server, files, "Codecks returned invalid upload instructions.");
        }

        [UnityTest]
        public IEnumerator CreateNewCard_UploadHttpError_CompletesOnceWithFailure()
        {
            LoopbackServer server = null;
            server = new LoopbackServer(request => request.Path == "/create"
                ? Response.Json("{\"ok\":true,\"cardId\":\"card\",\"uploadUrls\":[{\"fileName\":\"report.txt\",\"url\":\"" + server.Url + "upload\",\"fields\":{}}]}")
                : Response.Status(500));
            using (server)
            {
                var files = new Dictionary<string, (byte[], CodecksCardCreator.CodecksFileType)>
                {
                    ["report.txt"] = (new byte[] { 1 }, CodecksCardCreator.CodecksFileType.PlainText)
                };
                yield return AssertFailure(server, files, "error uploading file report.txt: ProtocolError");
                Assert.That(server.Requests.Length, Is.EqualTo(2));
            }
        }

        [UnityTest]
        public IEnumerator CreateNewCard_DestroyedDuringCreate_CompletesOnceWithCancellation()
        {
            using var server = new LoopbackServer(_ => Response.Json("{\"ok\":true,\"cardId\":\"late\"}", 1000));
            yield return DestroyDuringRequest(server, null, 1);
        }

        [UnityTest]
        public IEnumerator CreateNewCard_DisabledDuringCreate_ContinuesToCompletion()
        {
            using var server = new LoopbackServer(_ => Response.Json("{\"ok\":true,\"cardId\":\"disabled\"}", 100));
            var host = new GameObject("Codecks disable test");
            var creator = host.AddComponent<CodecksCardCreator>();
            creator.codecksURL = server.Url + "create";
            creator.defaultToken = "loopback-token";
            bool completed = false;
            bool success = false;
            try
            {
                creator.CreateNewCard("report body is long enough", CodecksCardCreator.CodecksSeverity.None, null,
                    (wasSuccessful, _) =>
                    {
                        completed = true;
                        success = wasSuccessful;
                    });
                while (server.Requests.Length == 0)
                    yield return null;

                creator.enabled = false;
                float deadline = Time.realtimeSinceStartup + 10f;
                while (!completed && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.That(completed, Is.True);
                Assert.That(success, Is.True);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator CreateNewCard_DestroyedDuringUpload_CompletesOnceWithCancellation()
        {
            LoopbackServer server = null;
            server = new LoopbackServer(request => request.Path == "/create"
                ? Response.Json("{\"ok\":true,\"cardId\":\"late\",\"uploadUrls\":[{\"fileName\":\"report.txt\",\"url\":\"" + server.Url + "upload\",\"fields\":{}}]}")
                : Response.Status(204, 1000));
            using (server)
            {
                var files = new Dictionary<string, (byte[], CodecksCardCreator.CodecksFileType)>
                {
                    ["report.txt"] = (new byte[] { 1 }, CodecksCardCreator.CodecksFileType.PlainText)
                };
                yield return DestroyDuringRequest(server, files, 2);
            }
        }

        private static IEnumerator DestroyDuringRequest(LoopbackServer server,
            Dictionary<string, (byte[], CodecksCardCreator.CodecksFileType)> files, int expectedRequestCount)
        {
            var host = new GameObject("Codecks destruction test");
            var creator = host.AddComponent<CodecksCardCreator>();
            creator.codecksURL = server.Url + "create";
            creator.defaultToken = "loopback-token";
            int callbackCount = 0;
            bool success = true;
            string result = null;
            creator.CreateNewCard("report body is long enough", files, resultDelegate: (wasSuccessful, response) =>
            {
                callbackCount++;
                success = wasSuccessful;
                result = response;
            });

            float deadline = Time.realtimeSinceStartup + 10f;
            while (server.Requests.Length < expectedRequestCount && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(server.Requests.Length, Is.EqualTo(expectedRequestCount));
            UnityEngine.Object.Destroy(host);
            yield return null;
            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(success, Is.False);
            Assert.That(result, Is.EqualTo("Codecks report request was cancelled because its creator was destroyed."));
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(callbackCount, Is.EqualTo(1), "A completed operation must not report a late response.");
        }

        private static IEnumerator AssertFailure(LoopbackServer server,
            Dictionary<string, (byte[], CodecksCardCreator.CodecksFileType)> files, string expectedResult)
        {
            yield return RunRequest(server, files, CodecksCardCreator.CodecksSeverity.None, null,
                (success, result, callbackCount) =>
                {
                    Assert.That(success, Is.False);
                    Assert.That(result, Is.EqualTo(expectedResult));
                    Assert.That(callbackCount, Is.EqualTo(1));
                });
        }

        private static IEnumerator RunRequest(LoopbackServer server,
            Dictionary<string, (byte[], CodecksCardCreator.CodecksFileType)> files,
            CodecksCardCreator.CodecksSeverity severity, string email,
            Action<bool, string, int> verify)
        {
            var host = new GameObject("Codecks loopback test");
            var creator = host.AddComponent<CodecksCardCreator>();
            creator.codecksURL = server.Url + "create";
            creator.defaultToken = "loopback-token";
            int callbackCount = 0;
            bool completed = false;
            bool success = false;
            string result = null;
            try
            {
                creator.CreateNewCard("report body is long enough", files, severity, email, (wasSuccessful, response) =>
                {
                    callbackCount++;
                    success = wasSuccessful;
                    result = response;
                    completed = true;
                });

                float deadline = Time.realtimeSinceStartup + 10f;
                while (!completed && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.That(completed, Is.True, "Loopback request did not complete within ten seconds.");
                yield return null;
                yield return null;
                verify(success, result, callbackCount);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        private sealed class LoopbackServer : IDisposable
        {
            private readonly HttpListener listener = new HttpListener();
            private readonly Func<Request, Response> responder;
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
            private readonly Task acceptTask;
            private readonly ConcurrentQueue<Request> requests = new ConcurrentQueue<Request>();

            public LoopbackServer(Func<Request, Response> responder)
            {
                this.responder = responder;
                int port;
                var socket = new TcpListener(IPAddress.Loopback, 0);
                try
                {
                    socket.Start();
                    port = ((IPEndPoint)socket.LocalEndpoint).Port;
                }
                finally
                {
                    socket.Stop();
                }

                Url = "http://127.0.0.1:" + port + "/";
                listener.Prefixes.Add(Url);
                listener.Start();
                acceptTask = Task.Run(AcceptLoop);
            }

            public string Url { get; }
            public Request[] Requests => requests.ToArray();

            public void Dispose()
            {
                cancellation.Cancel();
                listener.Close();
                try
                {
                    acceptTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException)
                {
                    // Closing HttpListener interrupts GetContextAsync.
                }
                cancellation.Dispose();
            }

            private async Task AcceptLoop()
            {
                while (!cancellation.IsCancellationRequested)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await listener.GetContextAsync();
                    }
                    catch (HttpListenerException) when (cancellation.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (ObjectDisposedException) when (cancellation.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                        {
                            var request = new Request(context.Request.Url.AbsolutePath, reader.ReadToEnd());
                            requests.Enqueue(request);
                            Response response = responder(request);
                            if (response.DelayMilliseconds > 0)
                                await Task.Delay(response.DelayMilliseconds, cancellation.Token);
                            context.Response.StatusCode = response.StatusCode;
                            if (response.Body != null)
                            {
                                byte[] body = Encoding.UTF8.GetBytes(response.Body);
                                context.Response.ContentType = "application/json";
                                context.Response.ContentLength64 = body.Length;
                                await context.Response.OutputStream.WriteAsync(body, 0, body.Length);
                            }
                        }
                    }
                    finally
                    {
                        context.Response.Close();
                    }
                }
            }
        }

        private readonly struct Request
        {
            public Request(string path, string body)
            {
                Path = path;
                Body = body;
            }

            public string Path { get; }
            public string Body { get; }
        }

        private readonly struct Response
        {
            private Response(int statusCode, string json, int delayMilliseconds)
            {
                StatusCode = statusCode;
                Body = json;
                DelayMilliseconds = delayMilliseconds;
            }

            public int StatusCode { get; }
            public string Body { get; }
            public int DelayMilliseconds { get; }
            public static Response Json(string body, int delayMilliseconds = 0) => new Response(200, body, delayMilliseconds);
            public static Response Status(int statusCode, int delayMilliseconds = 0) => new Response(statusCode, null, delayMilliseconds);
        }
    }
}
