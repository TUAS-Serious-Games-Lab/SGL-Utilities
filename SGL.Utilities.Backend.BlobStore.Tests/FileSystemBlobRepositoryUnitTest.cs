using SGL.Utilities;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Backend.BlobStore.Tests {
	public class FileSystemBlobRepositoryUnitTestFixture : IDisposable {
		private readonly string storageDirectory = Path.Combine(Environment.CurrentDirectory, "TempTestData", "BlobStorage");
		public FileSystemBlobRepository FSStorage { get; set; }
		public IBlobRepository Storage => FSStorage;

		public FileSystemBlobRepositoryUnitTestFixture() {
			FSStorage = new FileSystemBlobRepository(storageDirectory);
		}

		public void Dispose() {
			Directory.Delete(storageDirectory, true);
		}
	}

	public class FileSystemBlobRepositoryUnitTest : IClassFixture<FileSystemBlobRepositoryUnitTestFixture> {
		private const string appName = "FileSystemBlobRepositoryUnitTest";
		private const string suffix = ".blob";
		private FileSystemBlobRepository FsStorage => fixture.FSStorage;
		private IBlobRepository Storage => fixture.Storage;
		private readonly ITestOutputHelper output;
		private readonly FileSystemBlobRepositoryUnitTestFixture fixture;

		public FileSystemBlobRepositoryUnitTest(ITestOutputHelper output, FileSystemBlobRepositoryUnitTestFixture fixture) {
			this.output = output;
			this.fixture = fixture;
		}

		private static MemoryStream MakeRandomTextContent() {
			var content = new MemoryStream();
			using (var writer = new StreamWriter(content, leaveOpen: true)) {
				for (int i = 0; i < 10; ++i) {
					writer.WriteLine(StringGenerator.GenerateRandomString(100));
				}
			}
			content.Position = 0;
			return content;
		}

		[Fact]
		public async Task BlobIsStoredAndRetrievedCorrectly() {
			BlobPath blobPath = new() { AppName = appName, OwnerId = Guid.NewGuid(), BlobId = Guid.NewGuid(), Suffix = suffix };
			using (var content = MakeRandomTextContent()) {
				await Storage.StoreBlobAsync(blobPath, content);
				content.Position = 0;
				using (var readStream = await Storage.ReadBlobAsync(blobPath)) {
					output.WriteStreamContents(readStream);
					readStream.Position = 0;
					using (var origReader = new StreamReader(content, leaveOpen: true))
					using (var readBackReader = new StreamReader(readStream, leaveOpen: true)) {
						Assert.Equal(origReader.EnumerateLines(), readBackReader.EnumerateLines());
					}
				}
			}
		}
		private async Task SeparationTest(BlobPath blobPathA, BlobPath blobPathB) {
			using (var contentA = MakeRandomTextContent())
			using (var contentB = MakeRandomTextContent()) {
				var taskA = Storage.StoreBlobAsync(blobPathA, contentA);
				var taskB = Storage.StoreBlobAsync(blobPathB, contentB);
				await Task.WhenAll(taskA, taskB);
				contentA.Position = 0;
				contentB.Position = 0;
				using (var readStreamA = await Storage.ReadBlobAsync(blobPathA))
				using (var readStreamB = await Storage.ReadBlobAsync(blobPathB)) {
					using (var origAReader = new StreamReader(contentA, leaveOpen: true))
					using (var readBackAReader = new StreamReader(readStreamA, leaveOpen: true))
					using (var origBReader = new StreamReader(contentB, leaveOpen: true))
					using (var readBackBReader = new StreamReader(readStreamB, leaveOpen: true)) {
						Assert.Equal(origAReader.EnumerateLines(), readBackAReader.EnumerateLines());
						Assert.Equal(origBReader.EnumerateLines(), readBackBReader.EnumerateLines());
					}
					readStreamA.Position = 0;
					readStreamB.Position = 0;
					using (var readBackAReader = new StreamReader(readStreamA, leaveOpen: true))
					using (var readBackBReader = new StreamReader(readStreamB, leaveOpen: true)) {
						Assert.NotEqual(readBackAReader.EnumerateLines(), readBackBReader.EnumerateLines());
					}
				}
			}
		}

		[Fact]
		public async Task BlobsWithSameIdAreSeparatedByUser() {
			BlobPath blobPathA = new() { AppName = appName, OwnerId = Guid.NewGuid(), BlobId = Guid.NewGuid(), Suffix = suffix };
			BlobPath blobPathB = new() { AppName = blobPathA.AppName, OwnerId = Guid.NewGuid(), BlobId = blobPathA.BlobId, Suffix = suffix };
			await SeparationTest(blobPathA, blobPathB);
		}

		[Fact]
		public async Task BlobsWithSameIdAndUserAreSeparatedByApp() {
			BlobPath blobPathA = new() { AppName = appName + "_A", OwnerId = Guid.NewGuid(), BlobId = Guid.NewGuid(), Suffix = suffix };
			BlobPath blobPathB = new() { AppName = appName + "_B", OwnerId = blobPathA.OwnerId, BlobId = blobPathA.BlobId, Suffix = suffix };
			await SeparationTest(blobPathA, blobPathB);
		}

		[Fact]
		public async Task CreatedBlobsAreCorrectlyEnumeratedForAppUser() {
			Guid userId = Guid.NewGuid();
			var positivePathList = new List<BlobPath>() {
				new BlobPath() { AppName = appName, OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName, OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName, OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName, OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix }
			};
			var negativePathList = new List<BlobPath>() {
				new BlobPath() { AppName = appName + "_A", OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName + "_B", OwnerId = Guid.NewGuid(), BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName, OwnerId = Guid.NewGuid(), BlobId = Guid.NewGuid(), Suffix = suffix }
			};
			using (var content = new MemoryStream()) {
				foreach (var p in positivePathList) {
					await Storage.StoreBlobAsync(p, content);
				}
				foreach (var p in negativePathList) {
					await Storage.StoreBlobAsync(p, content);
				}
			}
			Assert.All(positivePathList, p => Assert.Contains(p, Storage.EnumerateBlobs(appName, userId)));
			Assert.All(negativePathList, p => Assert.DoesNotContain(p, Storage.EnumerateBlobs(appName, userId)));
		}

		[Fact]
		public async Task CreatedBlobsAreCorrectlyEnumeratedForApp() {
			Guid userId = Guid.NewGuid();
			var positivePathList = new List<BlobPath>() {
				new BlobPath() { AppName = appName, OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName, OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName, OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName, OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName, OwnerId = Guid.NewGuid(), BlobId = Guid.NewGuid(), Suffix = suffix }
			};
			var negativePathList = new List<BlobPath>() {
				new BlobPath() { AppName = appName + "_A", OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName + "_B", OwnerId = Guid.NewGuid(), BlobId = Guid.NewGuid(), Suffix = suffix },
			};
			using (var content = new MemoryStream()) {
				foreach (var p in positivePathList) {
					await Storage.StoreBlobAsync(p, content);
				}
				foreach (var p in negativePathList) {
					await Storage.StoreBlobAsync(p, content);
				}
			}
			Assert.All(positivePathList, p => Assert.Contains(p, Storage.EnumerateBlobs(appName)));
			Assert.All(negativePathList, p => Assert.DoesNotContain(p, Storage.EnumerateBlobs(appName)));
		}

		[Fact]
		public async Task CreatedBlobsAreCorrectlyEnumeratedOverall() {
			Guid userId = Guid.NewGuid();
			var pathList = new List<BlobPath>() {
				new BlobPath() { AppName = appName, OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName, OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName, OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName, OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName, OwnerId = Guid.NewGuid(), BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName + "_A", OwnerId = userId, BlobId = Guid.NewGuid(), Suffix = suffix },
				new BlobPath() { AppName = appName + "_B", OwnerId = Guid.NewGuid(), BlobId = Guid.NewGuid(), Suffix = suffix },
			};
			using (var content = new MemoryStream()) {
				foreach (var p in pathList) {
					await Storage.StoreBlobAsync(p, content);
				}
			}
			Assert.All(pathList, p => Assert.Contains(p, Storage.EnumerateBlobs()));
		}

		[Fact]
		public async Task AttemptingToReadNonExistentBlobThrowsCorrectException() {
			var path = new BlobPath() { AppName = appName, OwnerId = Guid.NewGuid(), BlobId = Guid.NewGuid(), Suffix = suffix };
			var ex = await Assert.ThrowsAsync<BlobNotAvailableException>(async () => { await using (var stream = await Storage.ReadBlobAsync(path)) { } });
			Assert.Equal(path, ex.BlobPath);
		}

		[Fact]
		public async Task DeletedBlobIsNoLongerEnumerated() {
			var path = new BlobPath() { AppName = appName, OwnerId = Guid.NewGuid(), BlobId = Guid.NewGuid(), Suffix = suffix };
			using (var content = new MemoryStream()) {
				await Storage.StoreBlobAsync(path, content);
				Assert.Contains(path, Storage.EnumerateBlobs());
				await Storage.DeleteBlobAsync(path);
				Assert.DoesNotContain(path, Storage.EnumerateBlobs());
			}
		}

		[Fact]
		public async Task LastFinishedStoreWins() {
			var path = new BlobPath() { AppName = appName, OwnerId = Guid.NewGuid(), BlobId = Guid.NewGuid(), Suffix = suffix };

			using (var contentA = MakeRandomTextContent())
			using (var contentB = MakeRandomTextContent()) {
				using (var trigStreamA = new TriggeredBlockingStream(contentA))
				using (var trigStreamB = new TriggeredBlockingStream(contentB)) {
					var taskA = Storage.StoreBlobAsync(path, trigStreamA);
					var taskB = Storage.StoreBlobAsync(path, trigStreamB);
					trigStreamB.TriggerReadReady();
					await taskB;
					contentB.Position = 0;
					using (var readStream = await Storage.ReadBlobAsync(path)) {
						output.WriteStreamContents(readStream);
						readStream.Position = 0;
						using (var origReader = new StreamReader(contentB, leaveOpen: true))
						using (var readBackReader = new StreamReader(readStream, leaveOpen: true)) {
							Assert.Equal(origReader.EnumerateLines(), readBackReader.EnumerateLines());
						}
					}
					output.WriteLine("");
					trigStreamA.TriggerReadReady();
					await taskA;
					contentA.Position = 0;
					using (var readStream = await Storage.ReadBlobAsync(path)) {
						output.WriteStreamContents(readStream);
						readStream.Position = 0;
						using (var origReader = new StreamReader(contentA, leaveOpen: true))
						using (var readBackReader = new StreamReader(readStream, leaveOpen: true)) {
							Assert.Equal(origReader.EnumerateLines(), readBackReader.EnumerateLines());
						}
					}
				}
			}
		}
	}
}
