import {Component, OnDestroy, OnInit} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {Subscription} from 'rxjs';
import {
  CommonDictionaryModel,
} from 'src/app/common/models';
import {AuthStateService} from 'src/app/common/store';
import {PropertyAssignWorkersModel, DeviceUserModel, TaskWizardModel,} from '../../../../models';
import {BackendConfigurationPnPropertiesService} from '../../../../services';
import {
  PropertyWorkerCreateEditModalComponent
} from '../';
import {PropertyWorkersStateService} from '../store';
import {Sort} from '@angular/material/sort';
import {TranslateService} from '@ngx-translate/core';
import {ActivatedRoute, Router} from '@angular/router';
import {MatDialog} from '@angular/material/dialog';
import {Overlay} from '@angular/cdk/overlay';
import {dialogConfigHelper} from 'src/app/common/helpers';
import {tap} from 'rxjs/operators';
import * as R from 'ramda';
import {
  selectCurrentUserClaimsDeviceUsersCreate
} from 'src/app/state/auth/auth.selector';
import {Store} from '@ngrx/store';
import {
  selectPropertyWorkersFilters
} from '../../../../state/property-workers/property-workers.selector';

@AutoUnsubscribe()
@Component({
    selector: 'app-property-workers-page',
    templateUrl: './property-workers-page.component.html',
    standalone: false
})
export class PropertyWorkersPageComponent implements OnInit, OnDestroy {
  sitesDto: Array<DeviceUserModel>;
  availableProperties: CommonDictionaryModel[];
  workersAssignments: PropertyAssignWorkersModel[];

  getSites$: Subscription;
  getPropertiesDictionary$: Subscription;
  deviceUserAssignments$: Subscription;
  propertyWorkerEditModalComponentAfterClosedSub$: Subscription;
  propertyWorkerCreateModalComponentAfterClosedSub$: Subscription;
  getFiltersAsyncSub$: Subscription;
  public selectCurrentUserClaimsDeviceUsersCreate$ = this.store.select(selectCurrentUserClaimsDeviceUsersCreate);
  private selectPropertyWorkersFilters$ = this.store.select(selectPropertyWorkersFilters);

  constructor(
    private store: Store,
    private propertiesService: BackendConfigurationPnPropertiesService,
    public propertyWorkersStateService: PropertyWorkersStateService,
    private dialog: MatDialog,
    private overlay: Overlay,
  ) {
  }

  ngOnInit() {
    let propertyIds: number[] = [];
    this.getFiltersAsyncSub$ = this.selectPropertyWorkersFilters$
      .pipe(
        tap(filters => {
          if (filters.propertyIds.length !== 0 && !R.equals(propertyIds, filters.propertyIds)) {
            propertyIds = filters.propertyIds;
          }
          this.updateTable(propertyIds);
          // else {
          //   propertyIds = [];
          //   this.updateTable();
          // }
        },),
        tap(_ => {
          // if (this.showDiagram) {
          //   this.selectedPropertyId = this.taskWizardStateService.store.getValue().filters.propertyIds[0] || null;
          //   this.getPlannedTaskWorkers();
          // }
        })
      )
      .subscribe();
    //this.getWorkerPropertiesAssignments();
  }

  openEditModal(simpleSiteDto: DeviceUserModel) {
    const selectedSimpleSite = new DeviceUserModel();
    selectedSimpleSite.userFirstName = simpleSiteDto.userFirstName;
    selectedSimpleSite.userLastName = simpleSiteDto.userLastName;
    selectedSimpleSite.id = simpleSiteDto.siteUid;
    selectedSimpleSite.languageCode = simpleSiteDto.languageCode;
    selectedSimpleSite.normalId = simpleSiteDto.siteId;
    selectedSimpleSite.isLocked = simpleSiteDto.isLocked;
    selectedSimpleSite.timeRegistrationEnabled = simpleSiteDto.timeRegistrationEnabled;
    selectedSimpleSite.taskManagementEnabled = simpleSiteDto.taskManagementEnabled;
    selectedSimpleSite.hasWorkOrdersAssigned = simpleSiteDto.hasWorkOrdersAssigned;
    selectedSimpleSite.isBackendUser = simpleSiteDto.isBackendUser;
    selectedSimpleSite.startMonday = simpleSiteDto.startMonday;
    selectedSimpleSite.endMonday = simpleSiteDto.endMonday;
    selectedSimpleSite.breakMonday = simpleSiteDto.breakMonday;
    selectedSimpleSite.startTuesday = simpleSiteDto.startTuesday;
    selectedSimpleSite.endTuesday = simpleSiteDto.endTuesday;
    selectedSimpleSite.breakTuesday = simpleSiteDto.breakTuesday;
    selectedSimpleSite.startWednesday = simpleSiteDto.startWednesday;
    selectedSimpleSite.endWednesday = simpleSiteDto.endWednesday;
    selectedSimpleSite.breakWednesday = simpleSiteDto.breakWednesday;
    selectedSimpleSite.startThursday = simpleSiteDto.startThursday;
    selectedSimpleSite.endThursday = simpleSiteDto.endThursday;
    selectedSimpleSite.breakThursday = simpleSiteDto.breakThursday;
    selectedSimpleSite.startFriday = simpleSiteDto.startFriday;
    selectedSimpleSite.endFriday = simpleSiteDto.endFriday;
    selectedSimpleSite.breakFriday = simpleSiteDto.breakFriday;
    selectedSimpleSite.startSaturday = simpleSiteDto.startSaturday;
    selectedSimpleSite.endSaturday = simpleSiteDto.endSaturday;
    selectedSimpleSite.breakSaturday = simpleSiteDto.breakSaturday;
    selectedSimpleSite.startSunday = simpleSiteDto.startSunday;
    selectedSimpleSite.endSunday = simpleSiteDto.endSunday;
    selectedSimpleSite.breakSunday = simpleSiteDto.breakSunday;

    const workersAssignments = this.workersAssignments.find(
      (x) => x.siteId === simpleSiteDto.siteId
    );

    this.propertyWorkerEditModalComponentAfterClosedSub$ = this.dialog.open(PropertyWorkerCreateEditModalComponent,
      {
        ...dialogConfigHelper(this.overlay, {
          deviceUser: selectedSimpleSite,
          assignments: workersAssignments ? workersAssignments.assignments : [],
          availableProperties: this.availableProperties,
        }), minWidth: 500
      })
      .afterClosed().subscribe(data => data ? this.updateTable() : undefined);
  }

  openCreateModal() {
    this.propertyWorkerCreateModalComponentAfterClosedSub$ = this.dialog.open(PropertyWorkerCreateEditModalComponent,
      {
        ...dialogConfigHelper(this.overlay, {
          deviceUser: {},
          assignments: [],
          availableProperties: this.availableProperties,
        }), minWidth: 500
      })
      .afterClosed().subscribe(data => data ? this.updateTable() : undefined);
  }

  getPropertiesDictionary() {
    this.getPropertiesDictionary$ = this.propertiesService
      .getAllPropertiesDictionary()
      .subscribe((operation) => {
        if (operation && operation.success) {
          this.availableProperties = operation.model;
        }
      });
  }

  getWorkerPropertiesAssignments(propertyIds?: number[]) {
    this.deviceUserAssignments$ = this.propertiesService
      .getPropertiesAssignments(propertyIds)
      .subscribe((operation) => {
        if (operation && operation.success) {
          this.workersAssignments = [...operation.model];
        }
      });
  }

  getWorkerPropertyNames(siteId: number) {
    let resultString = '';
    if (this.workersAssignments) {
      const obj = this.workersAssignments.find((x) => x.siteId === siteId);
      if (obj) {
        obj.assignments
          .filter((x) => x.isChecked)
          .forEach((assignment) => {
            if (resultString.length !== 0) {
              resultString += '<br>';
            }
            resultString += this.availableProperties.find(
              (prop) => prop.id === assignment.propertyId
            ).name;
          });
      }
    }

    return resultString ?
      // @ts-ignore
      `<span title="${resultString.replaceAll('<br>', '\n')}">${resultString}</span>` :
      '--';
  }

  onSearchChanged(name: string) {
    this.propertyWorkersStateService.updateNameFilter(name);
    this.getDeviceUsersFiltered();
  }

  sortTable(sort: Sort) {
    this.propertyWorkersStateService.onSortTable(sort.active);
    this.getDeviceUsersFiltered();
  }

  getDeviceUsersFiltered(propertyIds?: number[]) {
    this.getSites$ = this.propertyWorkersStateService
      .getDeviceUsersFiltered()
      .subscribe((data) => {
        if (data && data.model) {
          // const result = data.model;
          this.sitesDto = data.model.map(site => ({
            ...site,
            propertyNames: Array.isArray(site.propertyNames)
              ? site.propertyNames
              : (typeof site.propertyNames === 'string' && site.propertyNames.length > 0
                ? site.propertyNames.split(',').map((name: string) => name.trim())
                : [])
          }));
          //this.getWorkerPropertiesAssignments();
        }
      });
  }



  updateTable(propertyIds?: number[]) {
    this.getPropertiesDictionary();
    this.getDeviceUsersFiltered(propertyIds);
    this.getWorkerPropertiesAssignments(propertyIds);
  }

  ngOnDestroy(): void {
  }
}
