import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  EntityItemModel,
  CommonDictionaryModel, DeviceUserRequestModel,
  OperationDataResult,
  OperationResult,
  Paged, FolderDto,
} from 'src/app/common/models';
import {
  PropertyCreateModel,
  PropertyModel,
  PropertiesRequestModel,
  PropertyUpdateModel,
  PropertyAreaModel,
  PropertyAreasUpdateModel, DeviceUserModel,
  PropertyAssignWorkersModel, ResultModel, ChrResultModel, PropertyFolderModel,
} from '../models';
import { ApiBaseService } from 'src/app/common/services';

export let BackendConfigurationPnPropertiesMethods = {
  Properties: 'api/backend-configuration-pn/properties',
  PropertyAreas: 'api/backend-configuration-pn/property-areas',
  PropertiesAssignment: 'api/backend-configuration-pn/properties/assignment',
  SimplePropertiesAssignment: 'api/backend-configuration-pn/properties/assignment/simple',
  PropertiesIndex: 'api/backend-configuration-pn/properties/index',
  UpdateDeviceUser: 'api/backend-configuration-pn/properties/assignment/update-device-user',
  CreateEntityList: 'api/backend-configuration-pn/property-areas/create-entity-list/',
  CreateDeviceUser: 'api/backend-configuration-pn/properties/assignment/create-device-user',
  GetAll: 'api/backend-configuration-pn/properties/assignment/index-device-user',
  GetCompanyType: 'api/backend-configuration-pn/properties/get-company-type',
  GetChrInformation: 'api/backend-configuration-pn/properties/get-chr-information',
  DictionaryProperties: 'api/backend-configuration-pn/properties/dictionary',
  GetFolderDtos: 'api/backend-configuration-pn/properties/get-folder-dtos',
  GetFolderList: 'api/backend-configuration-pn/properties/get-folder-list',
  GetLinkedSites: 'api/backend-configuration-pn/properties/get-linked-sites',
}

@Injectable({
  providedIn: 'root',
})
export class BackendConfigurationPnPropertiesService {
  constructor(private apiBaseService: ApiBaseService) {}

  getAllProperties(
    model: PropertiesRequestModel
  ): Observable<OperationDataResult<Paged<PropertyModel>>> {
    return this.apiBaseService.post(
      BackendConfigurationPnPropertiesMethods.PropertiesIndex,
      model
    );
  }

  getPropertyAreas(
    propertyId: number
  ): Observable<OperationDataResult<PropertyAreaModel[]>> {
    return this.apiBaseService.get(
      `${BackendConfigurationPnPropertiesMethods.PropertyAreas}`,
      { propertyId: propertyId }
    );
  }

  getAllPropertiesDictionary(fullNames: boolean = false): Observable<
    OperationDataResult<CommonDictionaryModel[]>
  > {
    return this.apiBaseService.get(
      BackendConfigurationPnPropertiesMethods.DictionaryProperties,
      {fullNames: false}
    );
  }

  createProperty(model: PropertyCreateModel): Observable<OperationResult> {
    return this.apiBaseService.post(
      BackendConfigurationPnPropertiesMethods.Properties,
      model
    );
  }

  updateProperty(model: PropertyUpdateModel): Observable<OperationResult> {
    return this.apiBaseService.put(
      BackendConfigurationPnPropertiesMethods.Properties,
      model
    );
  }

  updatePropertyAreas(
    model: PropertyAreasUpdateModel
  ): Observable<OperationResult> {
    return this.apiBaseService.put(
      BackendConfigurationPnPropertiesMethods.PropertyAreas,
      model
    );
  }

  assignPropertiesToWorker(
    model: PropertyAssignWorkersModel
  ): Observable<OperationResult> {
    return this.apiBaseService.post(
      BackendConfigurationPnPropertiesMethods.PropertiesAssignment,
      model
    );
  }

  updateAssignPropertiesToWorker(
    model: PropertyAssignWorkersModel
  ): Observable<OperationResult> {
    return this.apiBaseService.put(
      BackendConfigurationPnPropertiesMethods.PropertiesAssignment,
      model
    );
  }

  getPropertiesAssignments(propertyIds?: number[]): Observable<
    OperationDataResult<PropertyAssignWorkersModel[]>
  > {
    return this.apiBaseService.get(
      `${BackendConfigurationPnPropertiesMethods.PropertiesAssignment}`, { propertyIds: propertyIds }
    );
  }
  getSimplePropertiesAssignments(propertyIds?: number[]): Observable<
    OperationDataResult<PropertyAssignWorkersModel[]>
  > {
    return this.apiBaseService.get(
      `${BackendConfigurationPnPropertiesMethods.SimplePropertiesAssignment}`, { propertyIds: propertyIds }
    );
  }

  removeWorkerAssignments(deviceUserId: number): Observable<OperationResult> {
    return this.apiBaseService.delete(
      `${BackendConfigurationPnPropertiesMethods.PropertiesAssignment}`,
      { deviceUserId: deviceUserId }
    );
  }

  deleteProperty(propertyId: number): Observable<OperationResult> {
    return this.apiBaseService.delete(
      BackendConfigurationPnPropertiesMethods.Properties,
      { propertyId: propertyId }
    );
  }

  readProperty(id: number): Observable<OperationDataResult<PropertyModel>> {
    return this.apiBaseService.get(
      BackendConfigurationPnPropertiesMethods.Properties,
      { id: id }
    );
  }

  getChrInformation(id: number): Observable<OperationDataResult<ChrResultModel>> {
    return this.apiBaseService.get(
      BackendConfigurationPnPropertiesMethods.GetChrInformation,
      { cvrNumber: id }
    );
  }

  getCompanyType(id: number): Observable<OperationDataResult<ResultModel>> {
    return this.apiBaseService.get(
      BackendConfigurationPnPropertiesMethods.GetCompanyType,
      { cvrNumber: id }
    );
  }

  createSingleDeviceUser(
    model: DeviceUserModel
  ): Observable<OperationDataResult<number>> {
    return this.apiBaseService.put<DeviceUserModel>(
      BackendConfigurationPnPropertiesMethods.CreateDeviceUser,
      model
    );
  }

  updateSingleDeviceUser(model: DeviceUserModel): Observable<OperationResult> {
    return this.apiBaseService.post<DeviceUserModel>(
      BackendConfigurationPnPropertiesMethods.UpdateDeviceUser,
      model
    );
  }

  getDeviceUsersFiltered(
    model: DeviceUserRequestModel
  ): Observable<OperationDataResult<Array<DeviceUserModel>>> {
    return this.apiBaseService.post<Array<DeviceUserModel>>(
      BackendConfigurationPnPropertiesMethods.GetAll,
      model
    );
  }

  createEntityList(model: Array<EntityItemModel>, propertyAreaId: number): Observable<OperationResult> {
    return this.apiBaseService.post(
      BackendConfigurationPnPropertiesMethods.CreateEntityList + propertyAreaId,
      model
    );
  }

  getLinkedFolderDtos(id: number): Observable<OperationDataResult<PropertyFolderModel[]>> {
    return this.apiBaseService.get(
      BackendConfigurationPnPropertiesMethods.GetFolderDtos,
      { propertyId: id }
    );
  }

  getLinkedFolderDtosByMultipleProperties(ids: number[]): Observable<OperationDataResult<PropertyFolderModel[]>> {
    return this.apiBaseService.post(
      BackendConfigurationPnPropertiesMethods.GetFolderDtos,
      ids
    );
  }

  getLinkedFolderList(id: number): Observable<OperationDataResult<CommonDictionaryModel[]>> {
    return this.apiBaseService.get(
      BackendConfigurationPnPropertiesMethods.GetFolderList,
      { propertyId: id }
    );
  }

  getLinkedFolderListByMultipleProperties(ids: number[]): Observable<OperationDataResult<FolderDto[]>> {
    return this.apiBaseService.post(
      BackendConfigurationPnPropertiesMethods.GetFolderList,
      ids
    );
  }

  getLinkedSites(id: number, compliance: boolean): Observable<OperationDataResult<CommonDictionaryModel[]>> {
    return this.apiBaseService.get(
      BackendConfigurationPnPropertiesMethods.GetLinkedSites,
      { propertyId: id, compliance: compliance}
    );
  }

  getLinkedSitesByMultipleProperties(ids: number[]): Observable<OperationDataResult<CommonDictionaryModel[]>> {
    return this.apiBaseService.post(
      BackendConfigurationPnPropertiesMethods.GetLinkedSites,
      ids
    );
  }
}
