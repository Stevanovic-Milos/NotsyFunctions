using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notsy.DBModels;
using Notsy.Helpers;
using System.Net;
using System.Text.Json;

namespace Notsy
{
    public class Notes
    {
        private readonly ILogger<Notes> _logger;
        private readonly AppDbContext _dbContext;
        private readonly BlobStorageHelper _blobStorageHelper;
        private readonly ContentTypeHelper _contentTypeHelper;

        public Notes(ILogger<Notes> logger, AppDbContext dbContext, BlobStorageHelper blobStorageHelper, ContentTypeHelper contentTypeHelper)
        {
            _logger = logger;
            _dbContext = dbContext;
            _blobStorageHelper = blobStorageHelper;
            _contentTypeHelper = contentTypeHelper;
        }

        [Function("GetNotes")]
        public async Task<HttpResponseData> GetNotes([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequestData req, CancellationToken cancellationToken)
        {
            _logger.LogInformation("HTTP trigger function processed get notes a request.");
            try
            {
                var notes = await _dbContext.Notes.OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(notes, cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occured while retriving notes: {ex.Message}", cancellationToken);
                return errorResponse;
            }
        }
        [Function("GetCompletedNotes")]
        public async Task<HttpResponseData> GetCompletedNotes([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes/completed")] HttpRequestData req, CancellationToken cancellationToken)
        {
            _logger.LogInformation("HTTP trigger function processed get notes a request.");
            try
            {
                var notes = await _dbContext.Notes.Select(n => n).Where(n => n.Completed == true).OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(notes, cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occured while retriving completed notes: {ex.Message}", cancellationToken);
                return errorResponse;
            }
        }

        [Function("GetToDoNotes")]
        public async Task<HttpResponseData> GetToDoNotes([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes/todo")] HttpRequestData req, CancellationToken cancellationToken)
        {
            _logger.LogInformation("HTTP trigger function processed get notes a request.");
            try
            {
                var notes = await _dbContext.Notes.Select(n => n).Where(n => n.Completed == false).OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(notes, cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occured while retriving todo notes: {ex.Message}", cancellationToken);
                return errorResponse;
            }
        }


            [Function("GetNoteById")]
            public async Task<HttpResponseData> GetNoteById([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "note/{id:guid}")] HttpRequestData req, Guid id, CancellationToken cancellationToken)
            {
                try
                {
                    var note = await _dbContext.Notes.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

                    if (note == null)
                    {
                        var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                        await notFound.WriteStringAsync("Note not found",cancellationToken);
                        return notFound;
                    }

                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(note, cancellationToken);
                    return response;
                }
                catch (Exception ex)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteAsJsonAsync($"An error occured while retrieving note: {ex.Message}", cancellationToken);
                    return errorResponse;
                }
            }

        [Function("NewNote")]
        public async Task<HttpResponseData> WriteNote([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "note/create")] HttpRequestData req, CancellationToken cancellationToken)
        {
            _logger.LogInformation("HTTP trigger function processed post note a request.");
            try
            {
                // extracting note
                var note = await req.ReadFromJsonAsync<Note>(cancellationToken);

                if (note == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid body", cancellationToken);
                    return bad;
                }

                // Set default values
                note.CreatedAt = DateTime.UtcNow;
                note.Completed = false;
                note.CreatedBy = "System";

                // saving note using EF
                _dbContext.Notes.Add(note);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(note, cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occured while writing note: {ex.Message}", cancellationToken);
                return errorResponse;
            }
        }

        [Function("UpdateNote")]
        public async Task<HttpResponseData> UpdateNote([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "note/update/{id:guid}")] HttpRequestData req, Guid id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("HTTP trigger function processed update note request.");
            try
            {
                var updatedNote = await req.ReadFromJsonAsync<Note>(cancellationToken);

                if (updatedNote == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid body",cancellationToken);
                    return bad;
                }

                var existingNote = await _dbContext.Notes.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
                if (existingNote == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Note not found", cancellationToken);
                    return notFound;
                }

                existingNote.Title = updatedNote.Title;
                existingNote.Content = updatedNote.Content;
                existingNote.CreatedBy = updatedNote.CreatedBy;
                existingNote.Completed = updatedNote.Completed;
                existingNote.ImageUrl = updatedNote.ImageUrl;

                await _dbContext.SaveChangesAsync(cancellationToken);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(existingNote, cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occurred while updating note: {ex.Message}", cancellationToken);
                return errorResponse;
            }
        }

        [Function("CompleteNote")]
        public async Task<HttpResponseData> CompleteNote([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "note/complete/{id:guid}")] HttpRequestData req, Guid id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("HTTP trigger function processed update note request.");
            try
            {
                var updatedNote = await req.ReadFromJsonAsync<Note>(cancellationToken);

                if (updatedNote == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid body", cancellationToken);
                    return bad;
                }

                var existingNote = await _dbContext.Notes.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
                if (existingNote == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Note not found", cancellationToken);
                    return notFound;
                }

                existingNote.Completed = updatedNote.Completed;

                await _dbContext.SaveChangesAsync(cancellationToken);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(existingNote, cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occurred while completing note: {ex.Message}", cancellationToken);
                return errorResponse;
            }
        }

        [Function("UploadNoteImage")]
        public async Task<HttpResponseData> UploadNoteImage([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "note/{id:guid}/image")] HttpRequestData req,Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var note = await _dbContext.Notes.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
                if (note == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Note not found", cancellationToken);
                    return notFound;
                }

                using var memoryStream = new MemoryStream();
                await req.Body.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                if (memoryStream.Length == 0)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("No image provided", cancellationToken);
                    return badRequest;
                }

                var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault();
                var fileExtension = _contentTypeHelper.GetFileExtension(contentType!);

                var fileName = $"{id}_{Guid.NewGuid()}{fileExtension}";

                // Upload to BLOB storage
                var imageUrl = await _blobStorageHelper.UploadAsync(memoryStream, fileName, contentType!, cancellationToken);

                note.ImageUrl = imageUrl;
                await _dbContext.SaveChangesAsync(cancellationToken);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(note, cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"Error uploading image: {ex.Message}", cancellationToken);
                return errorResponse;
            }
        }

        [Function("DeleteNoteImage")]
        public async Task<HttpResponseData> DeleteNoteImage([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "note/{id:guid}/image")] HttpRequestData req,Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var note = await _dbContext.Notes.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

                if (note == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Note not found", cancellationToken);
                    return notFound;
                }

                if (string.IsNullOrEmpty(note.ImageUrl))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Note does not have an image", cancellationToken);
                    return badRequest;
                }

                // Remove image URL from note
                note.ImageUrl = null;
                await _dbContext.SaveChangesAsync(cancellationToken);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(note, cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occurred while deleting image: {ex.Message}", cancellationToken);
                return errorResponse;
            }
        }

        [Function("DeleteNote")]
        public async Task<HttpResponseData> DeleteNote([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "note/delete/{id:guid}")] HttpRequestData req, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var note = await _dbContext.Notes.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

                if (note == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Note not found", cancellationToken);
                    return notFound;
                }

                _dbContext.Notes.Remove(note);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(note, cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occured while deleting note: {ex.Message}", cancellationToken);
                return errorResponse;
            }
        }
    }
}