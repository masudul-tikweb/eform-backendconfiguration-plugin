using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BackendConfiguration.Pn.Infrastructure.Models.Documents;
using BackendConfiguration.Pn.Messages;
using BackendConfiguration.Pn.Services.BackendConfigurationLocalizationService;
using BackendConfiguration.Pn.Services.RebusService;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.eFormApi.BasePn.Infrastructure.Models.Common;
using Microting.EformBackendConfigurationBase.Infrastructure.Data;
using Microting.eFormCaseTemplateBase.Infrastructure.Data;
using Microting.eFormCaseTemplateBase.Infrastructure.Data.Entities;
using Rebus.Bus;
using CommonTranslationsModel = Microting.eForm.Infrastructure.Models.CommonTranslationsModel;

namespace BackendConfiguration.Pn.Services.BackendConfigurationDocumentService;

using Microting.eForm.Helpers;

public class BackendConfigurationDocumentService : IBackendConfigurationDocumentService
{
    private readonly CaseTemplatePnDbContext _caseTemplatePnDbContext;
    private readonly IEFormCoreService _coreHelper;
    private readonly IBackendConfigurationLocalizationService _backendConfigurationLocalizationService;
    private readonly BackendConfigurationPnDbContext _backendConfigurationPnDbContext;
    private readonly IBus _bus;
    private readonly IUserService _userService;

    public BackendConfigurationDocumentService(
	    CaseTemplatePnDbContext caseTemplatePnDbContext,
	    IEFormCoreService coreHelper,
	    IBackendConfigurationLocalizationService backendConfigurationLocalizationService,
	    BackendConfigurationPnDbContext backendConfigurationPnDbContext,
	    IRebusService rebusService,
	    IUserService userService
    )
    {
	    _caseTemplatePnDbContext = caseTemplatePnDbContext;
	    _coreHelper = coreHelper;
	    _backendConfigurationLocalizationService = backendConfigurationLocalizationService;
	    _backendConfigurationPnDbContext = backendConfigurationPnDbContext;
	    _userService = userService;
	    _bus = rebusService.GetBus();
    }

    public async Task<OperationDataResult<Paged<BackendConfigurationDocumentModel>>> Index(BackendConfigurationDocumentRequestModel pnRequestModel)
    {
        var query = _caseTemplatePnDbContext.Documents
            .Include(x => x.DocumentTranslations)
            .Include(x => x.DocumentProperties)
            .Include(x => x.DocumentUploadedDatas)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);


        // filtration
        if (pnRequestModel.PropertyId != -1)
        {
            query = query
                .Where(x => x.DocumentProperties.Count(y => y.PropertyId == pnRequestModel.PropertyId) > 0);
        }

        if (pnRequestModel.FolderId != null)
        {
            query = query.Where(x => x.FolderId == pnRequestModel.FolderId);
        }

        if (pnRequestModel.DocumentId != null)
        {
            query = query.Where(x => x.Id == pnRequestModel.DocumentId);
        }

        if (pnRequestModel.Expiration != null)
        {
            query = pnRequestModel.Expiration switch
            {
                0 => query.Where(x => x.EndAt <= DateTime.UtcNow),
                1 => query.Where(x => x.EndAt <= DateTime.UtcNow.AddDays(30) && x.EndAt > DateTime.UtcNow),
                2 => query.Where(x => x.EndAt > DateTime.UtcNow.AddDays(30)),
                _ => query
                /*2 => query.Where(x => x.EndAt <= DateTime.UtcNow.AddMonths(3)),
                3 => query.Where(x => x.EndAt <= DateTime.UtcNow.AddMonths(6)),
                4 => query.Where(x => x.EndAt <= DateTime.UtcNow.AddYears(1)),*/
            };
        }

        // sorting
        var excludeSort = new List<string> { /*"DocumentTranslations"*/ };
        query = QueryHelper.AddSortToQuery(query, pnRequestModel.Sort, pnRequestModel.IsSortDsc, excludeSort);

        // get total
        var total = await query.Select(x => x.Id).CountAsync().ConfigureAwait(false);

        var results = new List<BackendConfigurationDocumentModel>();

        // optimization. if total < 0 - we not need select data
        if (total > 0)
        {
	        results = await query
		        .Select(x => new BackendConfigurationDocumentModel
		        {
			        Id = x.Id,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
			        StartDate = x.StartAt,
			        EndDate = x.EndAt,
			        FolderId = x.FolderId,
			        Status = x.Status,
			        IsLocked = x.IsLocked,
			        DocumentUploadedDatas = x.DocumentUploadedDatas
				        .Where(y => y.WorkflowState != Constants.WorkflowStates.Removed)
				        .Select(y => new BackendConfigurationDocumentUploadedData
				        {
					        Id = y.Id,
					        DocumentId = y.DocumentId,
					        LanguageId = y.LanguageId,
					        Name = y.Name,
					        Hash = y.Hash,
					        FileName = y.File,
					        Extension = y.Extension,
                            UploadedDataId = _caseTemplatePnDbContext.DocumentUploadedDatas
                                .Where(e => e.WorkflowState != Constants.WorkflowStates.Removed)
                                .Where(e => e.DocumentId == y.DocumentId)
                                .Where(e => e.LanguageId == y.LanguageId)
                                .Where(e => e.Extension == y.Extension)
                                .Select(e => e.Id)
                                .FirstOrDefault()
                        }).ToList(),
			        DocumentTranslations = x.DocumentTranslations
				        .Where(y => y.WorkflowState != Constants.WorkflowStates.Removed)
				        .Select(y => new BackendConfigurationDocumentTranslationModel
				        {
					        Id = y.Id,
					        Name = y.Name,
					        Description = y.Description,
					        LanguageId = y.LanguageId,
					        ExtensionFile = y.ExtensionFile
                        })
                        .ToList(),
			        DocumentProperties = x.DocumentProperties
				        .Where(y => y.WorkflowState != Constants.WorkflowStates.Removed)
				        .Select(y => new BackendConfigurationDocumentProperty
				        {
					        Id = y.Id,
					        DocumentId = y.DocumentId,
					        PropertyId = y.PropertyId,
				        }).ToList()
		        })
		        .ToListAsync().ConfigureAwait(false);

            // filtration after select
            if (excludeSort.Contains(pnRequestModel.Sort))
            {
                results = QueryHelper.AddSortToQuery(results.AsQueryable(), pnRequestModel.Sort, pnRequestModel.IsSortDsc).ToList();
            }


            foreach (var backendConfigurationDocumentModel in results)
            {
                var propertyNames = "";
                foreach (var backendConfigurationDocumentProperty in backendConfigurationDocumentModel.DocumentProperties)
                {
                    if (propertyNames != "")
                    {
                        propertyNames += "<br>";
                    }
                    var property = await _backendConfigurationPnDbContext.Properties.FirstAsync(x =>
                        x.Id == backendConfigurationDocumentProperty.PropertyId);
                    backendConfigurationDocumentProperty.PropertyName = property.Name;
                    propertyNames += property.Name;
                }
                backendConfigurationDocumentModel.PropertyNames = propertyNames;
            }
        }
        return new OperationDataResult<Paged<BackendConfigurationDocumentModel>>(true,
            new Paged<BackendConfigurationDocumentModel> { Entities = results, Total = total });
    }

    public async Task<OperationDataResult<BackendConfigurationDocumentModel>> GetDocumentAsync(int id)
    {
        var document = await _caseTemplatePnDbContext.Documents
            .Include(x => x.DocumentTranslations)
            .Include(x => x.DocumentProperties)
            .Include(x => x.DocumentUploadedDatas. Where(y => y.WorkflowState != Constants.WorkflowStates.Removed))
            .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

        if (document == null)
        {
            return new OperationDataResult<BackendConfigurationDocumentModel>(false,
                _backendConfigurationLocalizationService.GetString("DocumentNotFound"));
        }

        var result = new BackendConfigurationDocumentModel
        {
            Id = document.Id,
            StartDate = document.StartAt,
            EndDate = document.EndAt,
            FolderId = document.FolderId,
            Status = document.Status,
            IsLocked = document.IsLocked,
            DocumentUploadedDatas = document.DocumentUploadedDatas
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Select(x => new BackendConfigurationDocumentUploadedData
                {
                    Id = x.Id,
                    DocumentId = x.DocumentId,
                    LanguageId = x.LanguageId,
                    Name = x.Name,
                    Hash = x.Hash,
                    FileName = x.File,
                    Extension = x.Extension,
                    UploadedDataId = x.Id
                }).ToList(),
            DocumentTranslations = document.DocumentTranslations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Select(x => new BackendConfigurationDocumentTranslationModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    LanguageId = x.LanguageId,
                    ExtensionFile = x.ExtensionFile
                }).ToList(),
            DocumentProperties = document.DocumentProperties
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Select(y => new BackendConfigurationDocumentProperty
                {
                    Id = y.Id,
                    DocumentId = y.DocumentId,
                    PropertyId = y.PropertyId
                }).ToList()
        };

        return new OperationDataResult<BackendConfigurationDocumentModel>(true, result);
    }

    public async Task<OperationDataResult<List<BackendConfigurationDocumentSimpleModel>>> GetDocuments(int languageId, int? propertyId)
    {
        var query = _caseTemplatePnDbContext.Documents
            .Include(x => x.DocumentTranslations)
            .Include(x => x.DocumentProperties)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

        if (propertyId != null)
        {
            query = query.Where(x => x.DocumentProperties.Any(y => y.PropertyId == propertyId));
        }
        var total = await query.Select(x => x.Id).CountAsync().ConfigureAwait(false);

        var results = new List<BackendConfigurationDocumentSimpleModel>();

        if (total > 0)
        {
            results = await query
                .Select(x => new BackendConfigurationDocumentSimpleModel
                {
                    Id = x.Id,
                    Name = x.DocumentTranslations.FirstOrDefault(y => y.LanguageId == languageId)!.Name
                })
                .OrderBy(x => x.Name)
                .ToListAsync().ConfigureAwait(false);
        }
        return new OperationDataResult<List<BackendConfigurationDocumentSimpleModel>>(true,
            results);
    }

    public async Task<OperationResult> UpdateDocumentAsync(BackendConfigurationDocumentModel model)
    {
        var core = await _coreHelper.GetCore();
        var document = await _caseTemplatePnDbContext.Documents
            .Include(x => x.DocumentTranslations)
            .Include(x => x.DocumentProperties)
            .Include(x => x.DocumentUploadedDatas)
            .Include(x => x.DocumentSites)
            .FirstOrDefaultAsync(x => x.Id == model.Id).ConfigureAwait(false);

        if (document == null)
        {
            return new OperationResult(false,
                _backendConfigurationLocalizationService.GetString("DocumentNotFound"));
        }

        // document.StartAt = model.StartDate;
        document.EndAt = model.EndDate;
        document.FolderId = model.FolderId;
        document.Status = model.Status;

        foreach (var translation in model.DocumentTranslations)
        {
            var documentTranslation = document.DocumentTranslations
                .FirstOrDefault(x => x.Id == translation.Id);

            if (documentTranslation == null)
            {
                return new OperationResult(false,
                    _backendConfigurationLocalizationService.GetString("DocumentTranslationNotFound"));
            }

            documentTranslation.Name = translation.Name;
            documentTranslation.Description = translation.Description;

            await documentTranslation.Update(_caseTemplatePnDbContext).ConfigureAwait(false);
        }
        document.IsLocked = document.Status;
        await document.Update(_caseTemplatePnDbContext).ConfigureAwait(false);

        var assignmentsForDelete = document.DocumentProperties
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .Where(x => !model.DocumentProperties.Select(y => y.PropertyId).Contains(x.PropertyId))
            .ToList();

        foreach (var documentProperty in assignmentsForDelete)
        {
            await documentProperty.Delete(_caseTemplatePnDbContext).ConfigureAwait(false);
        }

        var documentSites = _caseTemplatePnDbContext.DocumentSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .Where(x => x.DocumentId == model.Id).ToList();
        //var documentSites = document.DocumentSites.ToList();

        foreach (var documentSite in documentSites)
        {
            if (documentSite.SdkCaseId != 0)
            {
                await core.CaseDelete(documentSite.SdkCaseId);
            }

            await documentSite.Delete(_caseTemplatePnDbContext);
        }

        if (model.DocumentProperties != null)
        {
            foreach (var property in model.DocumentProperties)
            {
                var documentProperty = document.DocumentProperties
                    .FirstOrDefault(x => x.Id == property.Id);
                if (documentProperty == null)
                {
                    documentProperty = new DocumentProperty
                    {
                        DocumentId = document.Id,
                        PropertyId = property.PropertyId
                    };

                    await documentProperty.Create(_caseTemplatePnDbContext).ConfigureAwait(false);
                }
            }
        }

        foreach (var documentUploadedData in model.DocumentUploadedDatas)
        {
            var documentUploadedDataDb = document.DocumentUploadedDatas
                .FirstOrDefault(x => x.Id == documentUploadedData.Id);

            if (documentUploadedDataDb == null)
            {
                documentUploadedDataDb = new DocumentUploadedData
                {
                    DocumentId = document.Id,
                    LanguageId = documentUploadedData.LanguageId,
                    Name = documentUploadedData.Name,
                    Extension = documentUploadedData.Extension
                };

                await documentUploadedDataDb.Create(_caseTemplatePnDbContext).ConfigureAwait(false);
                var memoryStream = new MemoryStream();

                if (documentUploadedData.File != null)
                {
                    await documentUploadedData.File.CopyToAsync(memoryStream);
                    string checkSum;
                    using (var md5 = MD5.Create())
                    {
                        var grr = md5.ComputeHash(memoryStream.ToArray());
                        checkSum = BitConverter.ToString(grr).Replace("-", "").ToLower();
                    }

                    var fileName = checkSum + "." + documentUploadedDataDb.Name.Split(".")[^1];

                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await core.PdfUpload(memoryStream, checkSum, fileName);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await core.PutFileToS3Storage(memoryStream, fileName);
                    documentUploadedDataDb.File = fileName;
                    documentUploadedDataDb.Hash = checkSum;
                    documentUploadedDataDb.Name = documentUploadedData.Name;
                    await documentUploadedDataDb.Update(_caseTemplatePnDbContext).ConfigureAwait(false);

                    // if we have office document - we need convert doc to pdf and save pdf and doc
                    // user uploaded only pdf - save pdf.
                    // user uploaded pdf and doc - save pdf and doc.
                    // the user uploaded the doc - convert the doc to pdf and save them both
                    if (
                        (documentUploadedData.Name.Split(".")[^1] is "docx" or "docx")
                        && !model.DocumentUploadedDatas.Exists(x => x.Extension == "pdf" && x.LanguageId == documentUploadedData.LanguageId && !string.IsNullOrEmpty(x.Name)))
                    {
                        ReportHelper.ConvertToPdf(fileName, Path.Combine(Path.GetTempPath(), "results"));
                        await using FileStream fileStream = new(Path.Combine(Path.GetTempPath(), "results", $"{fileName.Split(".")[^1]}.pdf"), FileMode.Open, FileAccess.Read);
                        using var memoryStreamConvertedFile = new MemoryStream();
                        await fileStream.CopyToAsync(memoryStreamConvertedFile);

                        string checkSumConvertedFile;
                        using (var md5 = MD5.Create())
                        {
                            var grr = md5.ComputeHash(memoryStreamConvertedFile.ToArray());
                            checkSumConvertedFile = BitConverter.ToString(grr).Replace("-", "").ToLower();
                        }
                        // fileName = checkSum.extension
                        var fileNameConvertedFile = checkSumConvertedFile + ".pdf";

                        memoryStreamConvertedFile.Seek(0, SeekOrigin.Begin);
                        await core.PdfUpload(memoryStreamConvertedFile, checkSumConvertedFile, fileNameConvertedFile);
                        memoryStreamConvertedFile.Seek(0, SeekOrigin.Begin);
                        await core.PutFileToS3Storage(memoryStreamConvertedFile, fileNameConvertedFile);
                        var documentUploadedDataConvertedFileModel = new DocumentUploadedData
                        {
                            DocumentId = document.Id,
                            LanguageId = documentUploadedData.LanguageId,
                            Name = documentUploadedData.Name,
                            Extension = "pdf",
                            File = fileNameConvertedFile,
                            Hash = checkSumConvertedFile,
                            CreatedByUserId = _userService.UserId
                        };
                        await documentUploadedDataConvertedFileModel.Create(_caseTemplatePnDbContext);
                    }
                }
            }
            else
            {
                var documentUploadedDataModel = await _caseTemplatePnDbContext.DocumentUploadedDatas
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstAsync(x => x.DocumentId == document.Id
                                 && x.LanguageId == documentUploadedData.LanguageId
                                 && x.Extension == documentUploadedData.Extension)
                .ConfigureAwait(false);
                documentUploadedDataModel.Name = documentUploadedData.Name;
                var memoryStream = new MemoryStream();
                var memoryStream2 = new MemoryStream();

                if (documentUploadedData.File != null)
                {
                    await documentUploadedData.File.CopyToAsync(memoryStream);
                    string checkSum;
                    using (var md5 = MD5.Create())
                    {
                        var grr = md5.ComputeHash(memoryStream.ToArray());
                        checkSum = BitConverter.ToString(grr).Replace("-", "").ToLower();
                    }
				    // fileName = checkSum.extension
				    var fileName = checkSum + "." + documentUploadedDataModel.Name.Split(".")[^1];

                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await core.PdfUpload(memoryStream, checkSum, fileName);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await memoryStream.CopyToAsync(memoryStream2);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await core.PutFileToS3Storage(memoryStream, fileName);
                    documentUploadedDataModel.File = fileName;
                    documentUploadedDataModel.Hash = checkSum;
                    await documentUploadedDataModel.Update(_caseTemplatePnDbContext).ConfigureAwait(false);

				    // if we have office document - we need convert doc to pdf and save pdf and doc
				    // user uploaded only pdf - save pdf.
				    // user uploaded pdf and doc - save pdf and doc.
				    // the user uploaded the doc - convert the doc to pdf and save them both
				    if (
	                    (documentUploadedDataModel.Name.Split(".")[^1] is "docx" or "docx")
	                    && !model.DocumentUploadedDatas.Exists(x => x.Extension == "pdf" && x.LanguageId == documentUploadedData.LanguageId && !string.IsNullOrEmpty(x.Name)))
				    {
                        memoryStream2.Seek(0, SeekOrigin.Begin);
                        var downloadPath = Path.Combine(Path.GetTempPath(), "reports", "results");
                        Directory.CreateDirectory(downloadPath);
                        FileStream writeFileStream = new(Path.Combine(downloadPath, fileName), FileMode.Create, FileAccess.Write);
                        await memoryStream2.CopyToAsync(writeFileStream);
                        writeFileStream.Close();
					    ReportHelper.ConvertToPdf(Path.Combine(Path.GetTempPath(), "reports", "results", fileName), downloadPath);
                        var pdfFileName = fileName.Replace(".docx", ".pdf").Replace(".doc", ".pdf");
                        await using FileStream fileStream = new(Path.Combine(downloadPath, pdfFileName), FileMode.Open, FileAccess.Read);
					    using var memoryStreamConvertedFile = new MemoryStream();
					    await fileStream.CopyToAsync(memoryStreamConvertedFile);

					    string checkSumConvertedFile;
					    using (var md5 = MD5.Create())
					    {
						    var grr = md5.ComputeHash(memoryStreamConvertedFile.ToArray());
						    checkSumConvertedFile = BitConverter.ToString(grr).Replace("-", "").ToLower();
					    }
					    // fileName = checkSum.extension
					    var fileNameConvertedFile = checkSumConvertedFile + ".pdf";

					    memoryStreamConvertedFile.Seek(0, SeekOrigin.Begin);
					    await core.PdfUpload(memoryStreamConvertedFile, checkSumConvertedFile, fileNameConvertedFile);
					    memoryStreamConvertedFile.Seek(0, SeekOrigin.Begin);
					    await core.PutFileToS3Storage(memoryStreamConvertedFile, fileNameConvertedFile);
                        var documentUploadedDataConvertedFileModel = await _caseTemplatePnDbContext.DocumentUploadedDatas
                            .FirstAsync(x => x.DocumentId == document.Id
                                             && x.LanguageId == documentUploadedData.LanguageId
                                             && x.Extension == "pdf")
                            .ConfigureAwait(false);

                        documentUploadedDataConvertedFileModel.File = fileNameConvertedFile;
                        documentUploadedDataConvertedFileModel.Hash = checkSumConvertedFile;
                        documentUploadedDataConvertedFileModel.Name = model.DocumentTranslations.First(x => x.ExtensionFile == "docx").Name;
                        await documentUploadedDataConvertedFileModel.Update(_caseTemplatePnDbContext).ConfigureAwait(false);

                        var documentTranslation = _caseTemplatePnDbContext.DocumentTranslations
                            .First(x => x.DocumentId == document.Id
                                        && x.LanguageId == documentUploadedData.LanguageId
                                        && x.ExtensionFile == "pdf");

                        documentTranslation.ExtensionFile = "pdf";
                        documentTranslation.Name = model.DocumentTranslations.First(x => x.ExtensionFile == "docx").Name;
                        await documentTranslation.Update(_caseTemplatePnDbContext).ConfigureAwait(false);
                    }
                }
                else if (documentUploadedData.UploadedDataId != null)
                {
                    // if documentUploadedData.Id == null
                    var hash = await _caseTemplatePnDbContext.DocumentUploadedDatas
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.Id == documentUploadedData.UploadedDataId)
                        .Select(x => x.Hash)
                        .FirstOrDefaultAsync();
                    documentUploadedDataModel.Hash = hash;
                    documentUploadedDataModel.File = documentUploadedData.FileName;
                    documentUploadedDataModel.UpdatedByUserId = _userService.UserId;
                    await documentUploadedDataModel.Update(_caseTemplatePnDbContext);
                } else {
                    documentUploadedDataModel.Name = documentUploadedData.Name;
                    documentUploadedDataModel.File = documentUploadedData.FileName;
                    documentUploadedDataModel.Hash = documentUploadedData.Hash;
                    await documentUploadedDataModel.Update(_caseTemplatePnDbContext);
                }

            }
        }

        if (model.DocumentProperties != null)
        {
            await _bus.SendLocal(new DocumentUpdated(document.Id)).ConfigureAwait(false);
        }
        else
        {
            document.IsLocked = false;
            document.Status = false;
            await document.Update(_caseTemplatePnDbContext).ConfigureAwait(false);
        }


        return new OperationResult(true,
            _backendConfigurationLocalizationService.GetString("DocumentUpdatedSuccessfully"));
    }

    public async Task<OperationResult> CreateDocumentAsync(BackendConfigurationDocumentModel model)
    {
        if (model.FolderId == 0)
        {
            return new OperationResult(false,
                _backendConfigurationLocalizationService.GetString("FolderCannotBeEmpty"));
        }

        if (model.EndDate.Year == 1)
        {
            model.EndDate = DateTime.UtcNow.AddYears(10);
            // return new OperationResult(false,
            //     _backendConfigurationLocalizationService.GetString("EndDateCannotBeEmpty"));
        }

        var core = await _coreHelper.GetCore();
		var document = new Document
        {
            // StartAt = model.StartDate,
            EndAt = model.EndDate,
            FolderId = model.FolderId,
            Status = model.Status,
            IsLocked = model.Status,
            CreatedByUserId = _userService.UserId
        };

        await document.Create(_caseTemplatePnDbContext).ConfigureAwait(false);

        foreach (var translation in model.DocumentTranslations)
        {
            var documentTranslation = new DocumentTranslation
            {
                Name = translation.Name,
                Description = translation.Description,
                LanguageId = translation.LanguageId,
                DocumentId = document.Id,
                ExtensionFile = translation.ExtensionFile,
                CreatedByUserId = _userService.UserId
            };
            // if we have only docx file (without pdf) and pdf translate empty(maybe not need?) - get translation from docx for pdf
            if (documentTranslation.ExtensionFile == "pdf" && string.IsNullOrEmpty(documentTranslation.Name) && model.DocumentTranslations
                    .Exists(x => x.ExtensionFile == "docx" && x.LanguageId == documentTranslation.LanguageId && !string.IsNullOrEmpty(x.Name)))
            {
                documentTranslation.Name = model.DocumentUploadedDatas
                    .Where(x => x.Extension == "docx" && x.LanguageId == documentTranslation.LanguageId)
                    .Select(x => x.Name)
                    .FirstOrDefault();
            }

            await documentTranslation.Create(_caseTemplatePnDbContext).ConfigureAwait(false);
        }

        foreach (var documentUploadedData in model.DocumentUploadedDatas)
        {
            var documentUploadedDataModel = new DocumentUploadedData
            {
                DocumentId = document.Id,
                LanguageId = documentUploadedData.LanguageId,
                Name = documentUploadedData.Name,
                Extension = documentUploadedData.Extension,
                CreatedByUserId = _userService.UserId
            };
            // if we have only docx file (without pdf) - get name from docx for pdf
            if (documentUploadedData.Extension == "pdf" && string.IsNullOrEmpty(documentUploadedData.Name) && model.DocumentUploadedDatas
                    .Exists(x => x.Extension == "docx" && x.LanguageId == documentUploadedData.LanguageId && !string.IsNullOrEmpty(x.Name)))
            {
                documentUploadedDataModel.Name = model.DocumentUploadedDatas
                    .Where(x => x.Extension == "docx" && x.LanguageId == documentUploadedData.LanguageId)
                    .Select(x => x.Name.Replace(".docx", ".pdf").Replace(".doc", ".pdf"))
                    .FirstOrDefault();
            }

            await documentUploadedDataModel.Create(_caseTemplatePnDbContext).ConfigureAwait(false);
        }

        foreach (var documentUploadedData in model.DocumentUploadedDatas)
        {
            var documentUploadedDataModel = await _caseTemplatePnDbContext.DocumentUploadedDatas
                .FirstAsync(x => x.DocumentId == document.Id
                                 && x.LanguageId == documentUploadedData.LanguageId
                                 && x.Extension == documentUploadedData.Extension)
                .ConfigureAwait(false);
            var memoryStream = new MemoryStream();
            var memoryStream2 = new MemoryStream();

            if (documentUploadedData.File != null)
            {
                await documentUploadedData.File.CopyToAsync(memoryStream);
                string checkSum;
                using (var md5 = MD5.Create())
                {
                    var grr = md5.ComputeHash(memoryStream.ToArray());
                    checkSum = BitConverter.ToString(grr).Replace("-", "").ToLower();
                }
				// fileName = checkSum.extension
				var fileName = checkSum + "." + documentUploadedDataModel.Name.Split(".")[^1];

                memoryStream.Seek(0, SeekOrigin.Begin);
                await core.PdfUpload(memoryStream, checkSum, fileName);
                memoryStream.Seek(0, SeekOrigin.Begin);
                await memoryStream.CopyToAsync(memoryStream2);
                memoryStream.Seek(0, SeekOrigin.Begin);
                await core.PutFileToS3Storage(memoryStream, fileName);
                documentUploadedDataModel.File = fileName;
                documentUploadedDataModel.Hash = checkSum;
                await documentUploadedDataModel.Update(_caseTemplatePnDbContext).ConfigureAwait(false);

				// if we have office document - we need convert doc to pdf and save pdf and doc
				// user uploaded only pdf - save pdf.
				// user uploaded pdf and doc - save pdf and doc.
				// the user uploaded the doc - convert the doc to pdf and save them both
				if (
	                (documentUploadedDataModel.Name.Split(".")[^1] is "docx")
	                && !model.DocumentUploadedDatas.Exists(x => x.Extension == "pdf" && x.LanguageId == documentUploadedData.LanguageId && !string.IsNullOrEmpty(x.Name)))
				{
                    memoryStream2.Seek(0, SeekOrigin.Begin);
                    var downloadPath = Path.Combine(Path.GetTempPath(), "reports", "results");
                    Directory.CreateDirectory(downloadPath);
                    FileStream writeFileStream = new(Path.Combine(downloadPath, fileName), FileMode.Create, FileAccess.Write);
                    await memoryStream2.CopyToAsync(writeFileStream);
                    writeFileStream.Close();
					ReportHelper.ConvertToPdf(Path.Combine(Path.GetTempPath(), "reports", "results", fileName), downloadPath);
                    var pdfFileName = fileName.Replace(".docx", ".pdf").Replace(".doc", ".pdf");
                    await using FileStream fileStream = new(Path.Combine(downloadPath, pdfFileName), FileMode.Open, FileAccess.Read);
					using var memoryStreamConvertedFile = new MemoryStream();
					await fileStream.CopyToAsync(memoryStreamConvertedFile);

					string checkSumConvertedFile;
					using (var md5 = MD5.Create())
					{
						var grr = md5.ComputeHash(memoryStreamConvertedFile.ToArray());
						checkSumConvertedFile = BitConverter.ToString(grr).Replace("-", "").ToLower();
					}
					// fileName = checkSum.extension
					var fileNameConvertedFile = checkSumConvertedFile + ".pdf";

					memoryStreamConvertedFile.Seek(0, SeekOrigin.Begin);
					await core.PdfUpload(memoryStreamConvertedFile, checkSumConvertedFile, fileNameConvertedFile);
					memoryStreamConvertedFile.Seek(0, SeekOrigin.Begin);
					await core.PutFileToS3Storage(memoryStreamConvertedFile, fileNameConvertedFile);
                    var documentUploadedDataConvertedFileModel = await _caseTemplatePnDbContext.DocumentUploadedDatas
                        .FirstAsync(x => x.DocumentId == document.Id
                                         && x.LanguageId == documentUploadedData.LanguageId
                                         && x.Extension == "pdf")
                        .ConfigureAwait(false);

                    documentUploadedDataConvertedFileModel.File = fileNameConvertedFile;
                    documentUploadedDataConvertedFileModel.Hash = checkSumConvertedFile;
                    await documentUploadedDataConvertedFileModel.Update(_caseTemplatePnDbContext).ConfigureAwait(false);

                    var documentTranslation = _caseTemplatePnDbContext.DocumentTranslations
                        .First(x => x.DocumentId == document.Id
                                    && x.LanguageId == documentUploadedData.LanguageId
                                    && x.ExtensionFile == "pdf");

                    documentTranslation.ExtensionFile = "pdf";
                    documentTranslation.Name = model.DocumentTranslations.First(x => x.ExtensionFile == "docx" && x.LanguageId == documentUploadedData.LanguageId).Name;
                    await documentTranslation.Update(_caseTemplatePnDbContext).ConfigureAwait(false);
                }
            }
		}

        if (model.DocumentProperties != null)
        {
            foreach (var property in model.DocumentProperties)
            {
                var documentProperty = new DocumentProperty
                {
                    DocumentId = document.Id,
                    PropertyId = property.PropertyId
                };

                await documentProperty.Create(_caseTemplatePnDbContext).ConfigureAwait(false);
            }

            await _bus.SendLocal(new DocumentUpdated(document.Id)).ConfigureAwait(false);
        }
        else
        {
            document.IsLocked = false;
            document.Status = false;
            await document.Update(_caseTemplatePnDbContext).ConfigureAwait(false);
        }

        return new OperationResult(true,
            _backendConfigurationLocalizationService.GetString("DocumentCreatedSuccessfully"));
    }

    public async Task<OperationResult> DeleteDocumentAsync(int id)
    {
        var document = await _caseTemplatePnDbContext.Documents
            .Include(x => x.DocumentTranslations)
            .Include(x => x.DocumentProperties)
            .Include(x => x.DocumentUploadedDatas)
            .Include(x => x.DocumentSites)
            .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

        if (document == null)
        {
            return new OperationResult(false,
                _backendConfigurationLocalizationService.GetString("DocumentNotFound"));
        }

        foreach (var translation in document.DocumentTranslations)
        {
	        translation.UpdatedByUserId = _userService.UserId;
            await translation.Delete(_caseTemplatePnDbContext).ConfigureAwait(false);
        }

        var core = await _coreHelper.GetCore();

        foreach (var documentSite in document.DocumentSites.Where(x => x.WorkflowState != Constants.WorkflowStates.Removed))
        {
            if (documentSite.SdkCaseId != 0)
            {
                await core.CaseDelete(documentSite.SdkCaseId);
			}

            documentSite.UpdatedByUserId = _userService.UserId;
            await documentSite.Delete(_caseTemplatePnDbContext);
        }

        document.UpdatedByUserId = _userService.UserId;
        document.Status = false;
        await document.Update(_caseTemplatePnDbContext).ConfigureAwait(false);
		await document.Delete(_caseTemplatePnDbContext).ConfigureAwait(false);

        return new OperationResult(true,
            _backendConfigurationLocalizationService.GetString("DocumentDeletedSuccessfully"));
    }

    public async Task<OperationDataResult<Paged<BackendConfigurationDocumentFolderModel>>> GetFolders(BackendConfigurationDocumentFolderRequestModel pnRequestModel)
    {

        var currentUserLanguage = await _userService.GetCurrentUserLanguage().ConfigureAwait(false);

        var folderNameIds = await _caseTemplatePnDbContext.FolderTranslations
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .Where(x => x.LanguageId == currentUserLanguage.Id)
            .OrderBy(x => x.Name)
            .Select(x => x.FolderId)
            .ToListAsync().ConfigureAwait(false);

        var query = _caseTemplatePnDbContext.Folders
            .Include(x => x.FolderTranslations)
            .Include(x => x.FolderProperties)
            .Where(x => folderNameIds.Contains(x.Id))
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

        if (pnRequestModel.FolderId != null)
        {
            query = query.Where(x => x.Id == pnRequestModel.FolderId);
        }

        var query2 = query.AsEnumerable()
            .OrderBy(x => folderNameIds.IndexOf(x.Id));

        var total = query2.Select(x => x.Id).Count();

        var results = new List<BackendConfigurationDocumentFolderModel>();

        if (total > 0)
        {
            results = query2
                .Select(x => new BackendConfigurationDocumentFolderModel
                {
                    Id = x.Id,
                    IsDeletable = !_caseTemplatePnDbContext.Documents
	                    .Where(y => y.WorkflowState != Constants.WorkflowStates.Removed)
	                    .Any(y => y.FolderId == x.Id), // if folder empty(no have documents) - folder is deletable
                    DocumentFolderTranslations = x.FolderTranslations
                        .Where(y => y.WorkflowState != Constants.WorkflowStates.Removed)
                        .Select(y => new BackendConfigurationDocumentFolderTranslationModel
                        {
                            Id = y.Id,
                            Name = y.Name,
                            Description = y.Description,
                            LanguageId = y.LanguageId
                        }).ToList(),
                    Properties = x.FolderProperties
                        .Where(y => y.WorkflowState != Constants.WorkflowStates.Removed)
                        .Select(y => new BackendConfigurationDocumentFolderPropertyModel
                        {
                            Id = y.Id,
                            SdkFolderId = y.SdkFolderId
                        }).ToList()
                })
                .ToList();
        }
        return new OperationDataResult<Paged<BackendConfigurationDocumentFolderModel>>(true,
            new Paged<BackendConfigurationDocumentFolderModel> { Entities = results, Total = total });
    }

    public async Task<OperationDataResult<BackendConfigurationDocumentFolderModel>> GetFolderAsync(int id)
    {
        var folder = await _caseTemplatePnDbContext.Folders
            .Include(x => x.FolderTranslations)
            .Include(x => x.FolderProperties)
            .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

        if (folder == null)
        {
            return new OperationDataResult<BackendConfigurationDocumentFolderModel>(false,
                _backendConfigurationLocalizationService.GetString("FolderNotFound"));
        }

        var result = new BackendConfigurationDocumentFolderModel
        {
            Id = folder.Id,
            IsDeletable = !_caseTemplatePnDbContext.Documents
	            .Where(y => y.WorkflowState != Constants.WorkflowStates.Removed)
	            .Any(y => y.FolderId == folder.Id), // if folder empty(no have documents) - folder is deletable
			DocumentFolderTranslations = folder.FolderTranslations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Select(x => new BackendConfigurationDocumentFolderTranslationModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    LanguageId = x.LanguageId,
                    FolderId = folder.Id
                }).ToList(),
            Properties = folder.FolderProperties
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Select(x => new BackendConfigurationDocumentFolderPropertyModel
                {
                    Id = x.Id,
                    SdkFolderId = x.SdkFolderId
                }).ToList()
        };

        return new OperationDataResult<BackendConfigurationDocumentFolderModel>(true, result);
    }

    public async Task<OperationDataResult<List<BackendConfigurationDocumentSimpleFolderModel>>> GetFolders(
        int languageId)
    {
        var folders = await _caseTemplatePnDbContext.Folders
            .Include(x => x.FolderTranslations)
            .Include(x => x.FolderProperties)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed).ToListAsync();

        var result = new List<BackendConfigurationDocumentSimpleFolderModel>();
        foreach (var folder in folders)
        {
            result.Add(new BackendConfigurationDocumentSimpleFolderModel
            {
                Id = folder.Id,
                Name = folder.FolderTranslations.Any(x => x.LanguageId == languageId) ? folder.FolderTranslations.First(x => x.LanguageId == languageId).Name : folder.FolderTranslations.First().Name
            });
        }

        result = result.OrderBy(x => x.Name).ToList();

        return new OperationDataResult<List<BackendConfigurationDocumentSimpleFolderModel>>(true, result);
    }

    public async Task<OperationResult> CreateFolder(BackendConfigurationDocumentFolderModel model)
    {
        var properties = await _backendConfigurationPnDbContext.Properties
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();

        var folder = new Folder();
        await folder.Create(_caseTemplatePnDbContext).ConfigureAwait(false);
        var folderTranslations = new List<CommonTranslationsModel>();
        foreach (var translation in model.DocumentFolderTranslations)
        {
            var folderTranslation = new FolderTranslation
            {
                Name = translation.Name,
                Description = translation.Description,
                LanguageId = translation.LanguageId,
                FolderId = folder.Id
            };
            folderTranslations.Add(new CommonTranslationsModel
            {
                Description = translation.Description,
                LanguageId = translation.LanguageId,
                Name = translation.Name
            });

            await folderTranslation.Create(_caseTemplatePnDbContext).ConfigureAwait(false);
        }
        var core = await _coreHelper.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();
        foreach (var property in properties)
        {
            var folders = await sdkDbContext.FolderTranslations.Join(sdkDbContext.Folders,
                folderTranslation => folderTranslation.FolderId,
                folder1 => folder1.Id,
                (folderTranslation, folder1) => new { folderTranslation, folder1 })
                .Where(x => x.folder1.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.folderTranslation.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.folder1.ParentId == property.FolderId)
                .Where(x => x.folderTranslation.Name == "26. Dokumenter")
                .Select(x => x.folder1)
                .FirstOrDefaultAsync().ConfigureAwait(false);

            int folderId;

            if (folders != null) {
                folderId = folders.Id;
            }
            else
            {
                var documentFolderTranslations = new List<CommonTranslationsModel>
                {
                    new()
                    {
                        Description = "",
                        LanguageId = 1,
                        Name = "26. Dokumenter"
                    },
                    new()
                    {
                        Description = "",
                        LanguageId = 2,
                        Name = "26. Documents"
                    },
                    new()
                    {
                        Description = "",
                        LanguageId = 3,
                        Name = "26. Dokumenten"
                    }
                };

                folderId = await core.FolderCreate(documentFolderTranslations, property.FolderId).ConfigureAwait(false);
            }

            folderId = await core.FolderCreate(folderTranslations, folderId).ConfigureAwait(false);

            var folderProperty = new FolderProperty
            {
                FolderId = folder.Id,
                SdkFolderId = folderId,
                PropertyId = property.Id
            };
            await folderProperty.Create(_caseTemplatePnDbContext).ConfigureAwait(false);
        }

        return new OperationResult(true,
            _backendConfigurationLocalizationService.GetString("FolderCreatedSuccessfully"));
    }

    public async Task<OperationResult> UpdateFolder(BackendConfigurationDocumentFolderModel model)
    {
        var folder = await _caseTemplatePnDbContext.Folders
            .Include(x => x.FolderTranslations)
            .Include(x => x.FolderProperties)
            .FirstOrDefaultAsync(x => x.Id == model.Id).ConfigureAwait(false);

        if (folder == null)
        {
            return new OperationDataResult<BackendConfigurationDocumentFolderModel>(false,
                _backendConfigurationLocalizationService.GetString("FolderNotFound"));
        }

        var core = await _coreHelper.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();
        var folderTranslations = new List<CommonTranslationsModel>();

        foreach (var backendConfigurationDocumentFolderTranslationModel in model.DocumentFolderTranslations)
        {
            var translation = folder.FolderTranslations.FirstOrDefault(x => x.Id == backendConfigurationDocumentFolderTranslationModel.Id);
            if (translation == null)
            {
                translation = new FolderTranslation
                {
                    FolderId = folder.Id,
                    Name = backendConfigurationDocumentFolderTranslationModel.Name,
                    Description = backendConfigurationDocumentFolderTranslationModel.Description,
                    LanguageId = backendConfigurationDocumentFolderTranslationModel.LanguageId
                };
                await translation.Create(_caseTemplatePnDbContext).ConfigureAwait(false);
            }
            else
            {
                translation.Name = backendConfigurationDocumentFolderTranslationModel.Name;
                translation.Description = backendConfigurationDocumentFolderTranslationModel.Description;
                await translation.Update(_caseTemplatePnDbContext).ConfigureAwait(false);
            }
            folderTranslations.Add(new CommonTranslationsModel
            {
                Description = translation.Description,
                LanguageId = translation.LanguageId,
                Name = translation.Name
            });
        }

        foreach (var folderProperty in folder.FolderProperties)
        {
            var sdkFolder = await sdkDbContext.Folders.FirstAsync(x => x.Id == folderProperty.SdkFolderId).ConfigureAwait(false);
            await core.FolderUpdate(folderProperty.SdkFolderId, folderTranslations, sdkFolder.ParentId).ConfigureAwait(false);
        }

        return new OperationResult(true,
            _backendConfigurationLocalizationService.GetString("FolderUpdatedSuccessfully"));
    }

    public async Task<OperationResult> DeleteFolder(int id)
    {
        var folder = await _caseTemplatePnDbContext.Folders
            .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

        if (folder == null)
        {
            return new OperationResult(false,
                _backendConfigurationLocalizationService.GetString("DescriptionFolderNotFound"));
        }

        var isDeletable = !_caseTemplatePnDbContext.Documents
	        .Where(y => y.WorkflowState != Constants.WorkflowStates.Removed)
	        .Any(y => y.FolderId == id); // if folder empty(no have documents) - folder is deletable

        if (!isDeletable)
		{
			return new OperationResult(false,
				_backendConfigurationLocalizationService.GetString("FolderIsNotDeletable"));
		}

        folder.UpdatedByUserId = _userService.UserId;
		await folder.Delete(_caseTemplatePnDbContext);

        return new OperationResult(true,
            _backendConfigurationLocalizationService.GetString("DescriptionFolderDeletedSuccessfully"));
    }
}