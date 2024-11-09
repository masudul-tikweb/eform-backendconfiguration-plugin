﻿/*
The MIT License (MIT)

Copyright (c) 2007 - 2023 Microting A/S

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using Microting.EformBackendConfigurationBase.Infrastructure.Data.Entities;
using Sentry;

namespace BackendConfiguration.Pn.Services.BackendConfigurationTaskTrackerService;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BackendConfigurationLocalizationService;
using Infrastructure.Models.TaskTracker;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.EformBackendConfigurationBase.Infrastructure.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microting.ItemsPlanningBase.Infrastructure.Data;
using Infrastructure.Helpers;
using System.IO;
using ExcelService;

public class BackendConfigurationTaskTrackerService(
	IBackendConfigurationLocalizationService localizationService,
	IUserService userService,
	BackendConfigurationPnDbContext backendConfigurationPnDbContext,
	IEFormCoreService coreHelper,
	ItemsPlanningPnDbContext itemsPlanningPnDbContext,
	IExcelService excelService)
	: IBackendConfigurationTaskTrackerService
{
	public async Task<OperationDataResult<List<TaskTrackerModel>>> Index(TaskTrackerFiltrationModel filtersModel)
	{
		var userLanguageId = (await userService.GetCurrentUserLanguage()).Id;
		var result = await BackendConfigurationTaskTrackerHelper.Index(filtersModel, backendConfigurationPnDbContext,
			await coreHelper.GetCore(), userLanguageId, itemsPlanningPnDbContext);
		return new OperationDataResult<List<TaskTrackerModel>>(result.Success,
			localizationService.GetString(result.Message), result.Model ??
			                                               []);
	}

	public async Task<OperationDataResult<List<TaskTrackerColumn>>> GetColumns()
	{
		var userId = userService.UserId;
		try
		{
			var columns = await backendConfigurationPnDbContext.TaskTrackerColumns
				.Where(p => p.UserId == userId)
				.Select(p => new TaskTrackerColumn
				{
					ColumnName = p.ColumnName,
					isColumnEnabled = p.isColumnEnabled
				})
				.ToListAsync();
			return new OperationDataResult<List<TaskTrackerColumn>>(true, columns);
		}
		catch (Exception e)
		{
			SentrySdk.CaptureException(e);
			Log.LogException(e.Message);
			Log.LogException(e.StackTrace);
			return new OperationDataResult<List<TaskTrackerColumn>>(false,
				$"{localizationService.GetString("ErrorWhileGetColumns")}: {e.Message}");
		}
	}

	public async Task<OperationResult> UpdateColumns(List<TaskTrackerColumns> updatedColumns)
	{
		try
		{
			var userId = userService.UserId;

			foreach (var updatedColumn in updatedColumns)
			{
				var columnFromDb = await backendConfigurationPnDbContext.TaskTrackerColumns
					.Where(p => p.UserId == userId)
					.Where(p => p.ColumnName == updatedColumn.ColumnName)
					.FirstOrDefaultAsync();

				if (columnFromDb is null)
				{
					columnFromDb = new TaskTrackerColumn
					{
						isColumnEnabled = updatedColumn.IsColumnEnabled,
						ColumnName = updatedColumn.ColumnName,
						UserId = userId,
						CreatedByUserId = userId,
						UpdatedByUserId = userId
					};
					await columnFromDb.Create(backendConfigurationPnDbContext);

					continue;
				}

				if (columnFromDb.isColumnEnabled != updatedColumn.IsColumnEnabled)
				{
					columnFromDb.isColumnEnabled = updatedColumn.IsColumnEnabled;
					columnFromDb.UpdatedByUserId = userId;
					await columnFromDb.Update(backendConfigurationPnDbContext);
				}
			}

			return new OperationDataResult<List<TaskTrackerColumns>>(true,
				$"{localizationService.GetString("ColumnsUpdatedSuccessful")}");
		}
		catch (Exception e)
		{
			SentrySdk.CaptureException(e);
			Log.LogException(e.Message);
			Log.LogException(e.StackTrace);
			return new OperationResult(false,
				$"{localizationService.GetString("ErrorWhileUpdateColumns")}: {e.Message}");
		}
	}

	public async Task<OperationDataResult<Stream>> GenerateExcelReport(TaskTrackerFiltrationModel filtersModel)
	{
		try
		{
			var userLanguageId = (await userService.GetCurrentUserLanguage()).Id;
			var result = await BackendConfigurationTaskTrackerHelper.Index(filtersModel,
				backendConfigurationPnDbContext, await coreHelper.GetCore(), userLanguageId, itemsPlanningPnDbContext);
			if (!result.Success)
			{
				return new OperationDataResult<Stream>(false, localizationService.GetString(result.Message));
			}

			var report = await excelService.GenerateTaskTracker(result.Model);

			if (report == null)
			{
				return new OperationDataResult<Stream>(false,
					localizationService.GetString("ErrorWhileGeneratingReport"));
			}

			return new OperationDataResult<Stream>(true, report);
		}
		catch (Exception e)
		{
			SentrySdk.CaptureException(e);
			Log.LogException(e.Message);
			Log.LogException(e.StackTrace);
			return new OperationDataResult<Stream>(false,
				$"{localizationService.GetString("ErrorWhileGeneratingReport")}: {e.Message}");
		}
	}
}