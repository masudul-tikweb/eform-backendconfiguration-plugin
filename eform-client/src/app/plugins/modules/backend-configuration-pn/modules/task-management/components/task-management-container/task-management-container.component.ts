import {Component, OnDestroy, OnInit} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {AdHocTaskPrioritiesModel, AdHocTaskWorkers, PropertyModel, WorkOrderCaseModel} from '../../../../models';
import {TaskManagementStateService} from '../store';
import {
  TaskManagementCreateShowModalComponent,
  TaskManagementDeleteModalComponent
} from '../';
import {
  BackendConfigurationPnPropertiesService,
  BackendConfigurationPnTaskManagementService
} from '../../../../services';
import {saveAs} from 'file-saver';
import {ToastrService} from 'ngx-toastr';
import {Subscription} from 'rxjs';
import {CommonDictionaryModel, EntityItemModel} from 'src/app/common/models';
import {MatIconRegistry} from '@angular/material/icon';
import {DomSanitizer} from '@angular/platform-browser';
import {ExcelIcon, WordIcon} from 'src/app/common/const';
import {MatDialog, MatDialogRef} from '@angular/material/dialog';
import {Overlay} from '@angular/cdk/overlay';
import {dialogConfigHelper} from 'src/app/common/helpers';
import {EntitySelectService, LoaderService} from 'src/app/common/services';
import {catchError, skip, tap} from 'rxjs/operators';
import {StatisticsStateService} from '../../../statistics/store';
import {ActivatedRoute} from '@angular/router';
import {AreaRuleEntityListModalComponent} from 'src/app/plugins/modules/backend-configuration-pn/components';
import {Store} from '@ngrx/store';
import {
  selectTaskManagementFilters,
  selectTaskManagementPropertyId,
  selectTaskManagementPropertyIdIsNullOrUndefined
} from '../../../../state/task-management/task-management.selector';
import {
  selectStatisticsPropertyId
} from '../../../../state/statistics/statistics.selector';

@AutoUnsubscribe()
@Component({
    selector: 'app-task-management-container',
    templateUrl: './task-management-container.component.html',
    styleUrls: ['./task-management-container.component.scss'],
    standalone: false
})
export class TaskManagementContainerComponent implements OnInit, OnDestroy {
  workOrderCases: WorkOrderCaseModel[] = [];
  properties: CommonDictionaryModel[] = [];
  adHocTaskPrioritiesModel: AdHocTaskPrioritiesModel;
  adHocTaskWorkers: AdHocTaskWorkers;
  selectedPropertyId: number | null = null;
  selectedPriority: number | null = null;
  selectedStatuses: number[] = [];
  selectedWorkerId: number | null = null;
  view = [1000, 300];
  diagramForShow: string = '';

  downloadWordReportSub$: Subscription;
  downloadExcelReportSub$: Subscription;
  getAllWorkOrderCasesSub$: Subscription;
  getWorkOrderCaseSub$: Subscription;
  deleteWorkOrderCaseSub$: Subscription;
  workOrderCaseDeleteSub$: Subscription;
  taskCreatedSub$: Subscription;
  taskUpdatedSub$: Subscription;
  getAdHocTaskPrioritiesSub$: Subscription;
  getPropertyIdAsyncSub$: Subscription;
  getAdHocTaskWorkersSub$: Subscription;

  get propertyName(): string {
    if (this.properties && this.selectedPropertyId) {
      const index = this.properties.findIndex(x => x.id === this.selectedPropertyId);
      if (index !== -1) {
        return this.properties[index].name;
      }
    }
    return '';
  }
  public selectTaskManagementPropertyIdIsNullOrUndefined$ = this.store.select(selectTaskManagementPropertyIdIsNullOrUndefined);
  public selectTaskManagementPropertyId$ = this.store.select(selectTaskManagementPropertyId);
  private selectTaskManagementFilters$ = this.store.select(selectTaskManagementFilters);
  private selectStatisticsPropertyId$ = this.store.select(selectStatisticsPropertyId);

  constructor(
    private store: Store,
    private loaderService: LoaderService,
    public taskManagementStateService: TaskManagementStateService,
    public taskManagementService: BackendConfigurationPnTaskManagementService,
    private toasterService: ToastrService,
    private propertyService: BackendConfigurationPnPropertiesService,
    iconRegistry: MatIconRegistry,
    sanitizer: DomSanitizer,
    public dialog: MatDialog,
    private overlay: Overlay,
    private statisticsStateService: StatisticsStateService,
    private route: ActivatedRoute,
  ) {
    iconRegistry.addSvgIconLiteral('file-word', sanitizer.bypassSecurityTrustHtml(WordIcon));
    iconRegistry.addSvgIconLiteral('file-excel', sanitizer.bypassSecurityTrustHtml(ExcelIcon));
    this.route.queryParams.subscribe(x => {
      if (x && x.diagramForShow) {
        this.diagramForShow = x.diagramForShow;
        // this.selectTaskManagementPropertyId$.subscribe((propertyId) => {
        //   debugger;
        //   if (propertyId) {
        //     this.selectedPropertyId = propertyId;
        //   }
        // });
        this.selectTaskManagementFilters$.subscribe((filters) => {
          if (filters.propertyId) {
            this.selectedPropertyId = filters.propertyId;
          }
          this.selectedPriority = filters.priority;
          this.selectedStatuses = filters.statuses;
          this.selectedWorkerId = filters.lastAssignedTo;
        });
        this.updateTable();
        this.getStats();
      } else {
        this.diagramForShow = '';
      }
    });
    this.getPropertyIdAsyncSub$ = this.selectTaskManagementFilters$
      .pipe(skip(1))
      .subscribe(filters => {
        // if (filters.propertyId !== -1 && filters.propertyId !== this.selectedPropertyId) {
        //   this.selectedPropertyId = filters.propertyId;
          if (this.diagramForShow) {
            this.getStats();
          }
        // } else if (filters.propertyId === -1 && this.selectedPropertyId !== null) {
        //   this.selectedPropertyId = null;
        //   if (this.diagramForShow) {
        //     this.getStats();
        //   }
        // }
        this.updateTable();
      });
  }

  ngOnInit() {
    this.loaderService.setLoading(false);
    this.getProperties();
  }

  ngOnDestroy(): void {
  }

  updateTable(delayed: boolean = false) {
    this.getAllWorkOrderCasesSub$ = this.taskManagementStateService
      .getAllWorkOrderCases(delayed)
      .subscribe((data) => {
        if (data && data.success && data.model) {
          this.workOrderCases = data.model;
        }
      });
  }

  openViewModal(workOrderCaseId: number) {
    this.getWorkOrderCaseSub$ = this.taskManagementService
      .getWorkOrderCase(workOrderCaseId)
      .subscribe((data) => {
        if (data && data.success && data.model) {
          const updateModal = this.dialog.open(TaskManagementCreateShowModalComponent, {...dialogConfigHelper(this.overlay, data.model)});
          this.taskUpdatedSub$ = updateModal.componentInstance.taskCreated.subscribe(() => this.updateTable(true));
        }
      });
  }

  openCreateModal() {
    const createModal = this.dialog.open(TaskManagementCreateShowModalComponent, dialogConfigHelper(this.overlay));
    this.taskCreatedSub$ = createModal.componentInstance.taskCreated.subscribe(() => this.updateTable(false));
  }

  openDeleteModal(workOrderCaseModel: WorkOrderCaseModel) {
    const deleteModal = this.dialog.open(TaskManagementDeleteModalComponent, dialogConfigHelper(this.overlay, workOrderCaseModel));
    this.workOrderCaseDeleteSub$ = deleteModal.componentInstance.workOrderCaseDelete
      .subscribe(x => this.deleteWorkOrderCaseModel(x, deleteModal));
  }

  deleteWorkOrderCaseModel(workOrderCaseId: number, deleteModal: MatDialogRef<TaskManagementDeleteModalComponent>) {
    this.deleteWorkOrderCaseSub$ = this.taskManagementService
      .deleteWorkOrderCase(workOrderCaseId)
      .subscribe((data) => {
        if (data && data.success) {
          deleteModal.close();
          this.updateTable();
        }
      });
  }

  getProperties() {
    this.propertyService.getAllPropertiesDictionary()
      .subscribe((data) => {
        if (data && data.success && data.model) {
          this.properties = data.model;
        }
      });
  }

  onDownloadWordReport() {
    let currentFilters: any;
    this.selectTaskManagementFilters$.subscribe((filters) => {
      currentFilters = filters;
    }).unsubscribe();
    this.downloadWordReportSub$ = this.taskManagementStateService
      .downloadWordReport()
      .pipe(
        tap((data) => {if (currentFilters.propertyId === -1 || currentFilters.propertyId === null) {
          saveAs(data, `report.docx`);
        } else {
          saveAs(data, `${this.properties.find(x =>
          x.id === currentFilters.propertyId).name}${currentFilters.areaName ? '_' + currentFilters.areaName : ''}_report.docx`);
          }
        }),
        catchError((err, caught) => {
          this.toasterService.error('Error downloading report');
          return caught;
        })
      )
      .subscribe();
  }

  onDownloadExcelReport() {
    let currentFilters: any;
    this.selectTaskManagementFilters$.subscribe((filters) => {
      currentFilters = filters;
    }).unsubscribe();
    this.downloadExcelReportSub$ = this.taskManagementStateService
      .downloadExcelReport()
      .pipe(
        tap((data) => {
          if (currentFilters.propertyId === -1 || currentFilters.propertyId === null) {
            saveAs(data, `report.xlsx`);
          } else {
            saveAs(data, `${this.properties.find(x =>
            x.id === currentFilters.propertyId).name}${currentFilters.areaName ? '_' + currentFilters.areaName : ''}_report.xlsx`);
            }
        }),
        catchError((_, caught) => {
          this.toasterService.error('Error downloading report');
          return caught;
        }),
      )
      .subscribe();
  }

  getAdHocTaskPriorities() {
    this.getAdHocTaskPrioritiesSub$ = this.statisticsStateService.getAdHocTaskPriorities(
      this.selectedPropertyId, this.selectedPriority, null, null, this.selectedStatuses)
      .pipe(tap(model => {
        if (model && model.success && model.model) {
          this.adHocTaskPrioritiesModel = model.model;
        }
      }))
      .subscribe();
  }

  getAdHocTaskWorkers() {
    this.getAdHocTaskWorkersSub$ = this.statisticsStateService.getAdHocTaskWorkers(this.selectedPropertyId, this.selectedWorkerId)
      .pipe(tap(model => {
        if (model && model.success && model.model) {
          this.adHocTaskWorkers = model.model;
        }
      }))
      .subscribe();
  }

  getStats() {
    if (this.diagramForShow === 'ad-hoc-task-priorities') {
      this.getAdHocTaskPriorities();
    } else if (this.diagramForShow === 'ad-hoc-task-workers') {
      this.getAdHocTaskWorkers();
    }
  }


}
