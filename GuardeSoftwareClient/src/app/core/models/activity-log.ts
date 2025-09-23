
export interface ActivityLog{
    userId: number;
    logDate: Date;
    action: string;
    tableName: string;
    recordId: number;
    oldValue?: string;
    newValue?: string;
}
