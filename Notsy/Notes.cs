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
        public async Task<HttpResponseData> GetNotes([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequestData req)
        {
            _logger.LogInformation("HTTP trigger function processed get notes a request.");
            try
            {
                var notes = await _dbContext.Notes.OrderByDescending(n => n.CreatedAt).ToListAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(notes);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occured while retriving notes: {ex.Message}");
                return errorResponse;
            }
        }
        [Function("GetCompletedNotes")]
        public async Task<HttpResponseData> GetCompletedNotes([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes/completed")] HttpRequestData req)
        {
            _logger.LogInformation("HTTP trigger function processed get notes a request.");
            try
            {
                var notes = await _dbContext.Notes.Select(n => n).Where(n => n.Completed == true).OrderByDescending(n => n.CreatedAt).ToListAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(notes);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occured while retriving completed notes: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GetToDoNotes")]
        public async Task<HttpResponseData> GetToDoNotes([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes/todo")] HttpRequestData req)
        {
            _logger.LogInformation("HTTP trigger function processed get notes a request.");
            try
            {
                var notes = await _dbContext.Notes.Select(n => n).Where(n => n.Completed == false).OrderByDescending(n => n.CreatedAt).ToListAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(notes);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occured while retriving todo notes: {ex.Message}");
                return errorResponse;
            }
        }


        [Function("GetNoteById")]
        public async Task<HttpResponseData> GetNoteById([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "note/{id:guid}")] HttpRequestData req, Guid id)
        {
            try
            {
                var note = await _dbContext.Notes.FindAsync(id);

                if (note == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Note not found");
                    return notFound;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(note);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occured while retrieving note: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("NewNote")]
        public async Task<HttpResponseData> WriteNote([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "note/create")] HttpRequestData req)
        {
            _logger.LogInformation("HTTP trigger function processed post note a request.");
            try
            {
                // extracting note
                var note = await req.ReadFromJsonAsync<Note>();

                if (note == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid body");
                    return bad;
                }

                // Set default values
                note.CreatedAt = DateTime.UtcNow;
                note.Completed = false;
                note.CreatedBy = "System";

                // saving note using EF
                _dbContext.Notes.Add(note);
                await _dbContext.SaveChangesAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(note);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occured while writing note: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("UpdateNote")]
        public async Task<HttpResponseData> UpdateNote([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "note/update/{id:guid}")] HttpRequestData req, Guid id)
        {
            _logger.LogInformation("HTTP trigger function processed update note request.");
            try
            {
                var updatedNote = await req.ReadFromJsonAsync<Note>();

                if (updatedNote == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid body");
                    return bad;
                }

                var existingNote = await _dbContext.Notes.FindAsync(id);
                if (existingNote == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Note not found");
                    return notFound;
                }

                existingNote.Title = updatedNote.Title;
                existingNote.Content = updatedNote.Content;
                existingNote.CreatedBy = updatedNote.CreatedBy;
                existingNote.Completed = updatedNote.Completed;
                existingNote.ImageUrl = updatedNote.ImageUrl;

                await _dbContext.SaveChangesAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(existingNote);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occurred while updating note: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("CompleteNote")]
        public async Task<HttpResponseData> CompleteNote([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "note/complete/{id:guid}")] HttpRequestData req, Guid id)
        {
            _logger.LogInformation("HTTP trigger function processed update note request.");
            try
            {
                var updatedNote = await req.ReadFromJsonAsync<Note>();

                if (updatedNote == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid body");
                    return bad;
                }

                var existingNote = await _dbContext.Notes.FindAsync(id);
                if (existingNote == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Note not found");
                    return notFound;
                }

                existingNote.Completed = updatedNote.Completed;

                await _dbContext.SaveChangesAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(existingNote);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occurred while completing note: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("UploadNoteImage")]
        public async Task<HttpResponseData> UploadNoteImage([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "note/{id:guid}/image")] HttpRequestData req,Guid id)
        {
            try
            {
                var note = await _dbContext.Notes.FindAsync(id);
                if (note == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Note not found");
                    return notFound;
                }

                using var memoryStream = new MemoryStream();
                await req.Body.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                if (memoryStream.Length == 0)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("No image provided");
                    return badRequest;
                }

                var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault();
                var fileExtension = _contentTypeHelper.GetFileExtension(contentType!);

                var fileName = $"{id}_{Guid.NewGuid()}{fileExtension}";

                // Upload to BLOB storage
                var imageUrl = await _blobStorageHelper.UploadAsync(memoryStream, fileName, contentType!);

                note.ImageUrl = imageUrl;
                await _dbContext.SaveChangesAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(note);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"Error uploading image: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("DeleteNoteImage")]
        public async Task<HttpResponseData> DeleteNoteImage([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "note/{id:guid}/image")] HttpRequestData req,Guid id)
        {
            try
            {
                var note = await _dbContext.Notes.FindAsync(id);

                if (note == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Note not found");
                    return notFound;
                }

                if (string.IsNullOrEmpty(note.ImageUrl))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Note does not have an image");
                    return badRequest;
                }

                // Remove image URL from note
                note.ImageUrl = null;
                await _dbContext.SaveChangesAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(note);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occurred while deleting image: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("DeleteNote")]
        public async Task<HttpResponseData> DeleteNote([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "note/delete/{id:guid}")] HttpRequestData req, Guid id)
        {
            try
            {
                var note = await _dbContext.Notes.FindAsync(id);

                if (note == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Note not found");
                    return notFound;
                }

                _dbContext.Notes.Remove(note);
                await _dbContext.SaveChangesAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(note);
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync($"An error occured while deleting note: {ex.Message}");
                return errorResponse;
            }
        }
    }
}