export interface PropertyFolderModel {
  id: number;
  name: string;
  description?: string;
  parentId?: number;
  microtingUId?: number;
  children?: PropertyFolderModel[];
  isLocked: boolean;
  isEditable: boolean;
  managedByPlugin: boolean;
}
