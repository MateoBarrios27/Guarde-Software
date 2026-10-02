export interface ClientLockerHistory {
    id: number;
    recordType: 'locker' | 'spaceRequest';
    lockerIdentifier?: string;
    warehouseName: string;
    lockerType?: string;
    quantity?: number;
    requestedM3?: number;
    startDate: Date;
    endDate: Date | null; 
    notes: string | null;
}
