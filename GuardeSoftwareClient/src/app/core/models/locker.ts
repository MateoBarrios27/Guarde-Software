
export interface Locker{
    id: number;
    warehouseId: number;
    lockerTypeId: number;
    identifier: string;
    features: string;
    status: string;
    clientName?: string;
    clientNames?: string;
    clients?: LockerClientSummary[];
    rentalId?: number | null;
    isFreeSpace?: boolean;
}

export interface LockerClientSummary {
    id: number;
    fullName: string;
    paymentIdentifier: number;
}
