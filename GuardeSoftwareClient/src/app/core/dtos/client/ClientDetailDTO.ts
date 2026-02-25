import { LockerClientDetailDTO } from "../locker/LockerClientDetailDTO";
import { PhoneInputDto } from "../phone/PhoneInputDto";
import { SpaceRequestDetailDto } from "../rentalSpaceRequest/GetSpaceRequestDetailDto";


export interface ClientDetailDTO{

    id: number;
    paymentIdentifier: number;
    name: string;
    lastName: string;
    city: string;
    province: string;
    cuit: string;
    dni: string;
    registrationDate: Date;
    billingType?: string;
    billingTypeId?: number;

    //contact information
    email: string[];
    phones?: PhoneInputDto[];
    address: string;
    receiveCommunications: boolean;
    

    //payment and rental info
    ivaCondition: string;
    preferredPaymentMethod: string;
    increasePercentage: number;
    increaseFrequency: number;
    nextIncreaseDay: Date;
    nextPaymentDay: Date;
    balance: number;
    paymentStatus: string;
    rentAmount: number;
    totalPaid?: number;

    //locker info
    lockersList?: LockerClientDetailDTO[];
    spaceRequests?: SpaceRequestDetailDto[];
    contractedM3: number;
    notes: string;
    occupiedSpaces: number;

    // Increase data
    increaseFrequencyMonths?: number;
    initialAmount?: number;
}
