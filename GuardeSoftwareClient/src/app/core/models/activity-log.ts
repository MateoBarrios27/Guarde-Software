
export interface ActivityLog {
    id: number;
    userId: number;
    logDate: string | Date;
    action: string;
    tableName: string;
    recordId: number;
    oldValue?: string;
    newValue?: string;
    userName?: string;
    userDisplayName?: string;
}

export interface ActivityLogFilter {
    pageNumber: number;
    pageSize: number;
    area?: string;
    action?: string;
    userId?: number;
    fromDate?: string;
    toDate?: string;
    search?: string;
}

export interface ActivityLogPage {
    items: ActivityLog[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
}

export interface ActivityLogUser {
    id: number;
    userName: string;
    displayName: string;
}
