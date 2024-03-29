/*
* logic_module.c
*
* Created: 12/05/2013 13:00:25
*  Author: Victor
*/
#include "modulesManager.h"
#include "globals.h"
#include "DIGITAL.h"

#include "sysTimer.h"

#define MAX_LOGIC_DEVICES 32

typedef struct
{
	LOGIC_CONFIG_t* config;
	uint8_t* portPtr;
	uint8_t mask;
	uint16_t timerCounter;
	unsigned lastValue		: 1;	//LSB
	unsigned debouncerValue : 1;
}LOGIC_ELEM_t;

static struct
{
	OPERATION_HEADER_t header;
	LOGIC_READ_RESPONSE_MESSAGE_t response;
}logicResponse;

static _Bool waitingForResponseConf;

static LOGIC_ELEM_t logic_elems[MAX_LOGIC_DEVICES];
static uint8_t num_of_logic_elems;

static SYS_Timer_t logicTimer;
static uint8_t timerDivider;

static void logicTimerHandler(SYS_Timer_t *timer);
static _Bool proccessDigitalPortAction(uint16_t address, _Bool read, uint8_t value, uint8_t seconds, uint16_t sourceAddress);
static uint8_t findLogicElem(uint16_t deviceAddress);
static inline void sendDigitalResponse(uint16_t destinationAddress, uint16_t deviceAddress, uint8_t value);

void logicModule_Init()
{
	LOGIC_CONFIG_t* configPtr;
	uint8_t* portPtr;
	uint8_t mask;
	
	//Set responses opCodes
	logicResponse.header.opCode = LogicReadResponse;
	
	waitingForResponseConf = false;
	
	//EEPROM config loading
	if(!validConfiguration)
	return;
	
	num_of_logic_elems = runningConfiguration.raw[runningConfiguration.topConfiguration.dinamicIndex.configModule_Logic];			//First byte is number of configs
	configPtr		   = &runningConfiguration.raw[runningConfiguration.topConfiguration.dinamicIndex.configModule_Logic + 1];		//At second byte the list start
	
	if(num_of_logic_elems > MAX_LOGIC_DEVICES)
	{
		//TODO: SEND ERROR (MAX NUMBER OF DEVICES EXCEEDED)
		num_of_logic_elems = MAX_LOGIC_DEVICES;
	}
	
	for(uint8_t i = 0; i<num_of_logic_elems; i++)
	{
		portPtr = PORT_FROM_PINADDRESS(configPtr->pinAddress);
		mask = MASK_FROM_PINADDRESS(configPtr->pinAddress);

		if(configPtr->configBits.maskIO) //Output
		{
			HAL_GPIO_PORT_out(portPtr, mask);
			
			if(configPtr->configBits.defaultValue)
			HAL_GPIO_PORT_set(portPtr, mask);
			else
			HAL_GPIO_PORT_clr(portPtr,mask);
			
		}else
		{
			HAL_GPIO_PORT_in(portPtr, mask);
		}
		
		logic_elems[i].config = configPtr;
		logic_elems[i].portPtr = portPtr;
		logic_elems[i].mask = mask;
		logic_elems[i].timerCounter = 0;
		configPtr++;
	}
	
	if(num_of_logic_elems > 0)
	{
		//Configure Timer
		timerDivider = 0;
	
		logicTimer.interval = 50; // 20 times per second
		logicTimer.mode = SYS_TIMER_PERIODIC_MODE;
		logicTimer.handler = logicTimerHandler;
		SYS_TimerStart(&logicTimer);		
	}
}

void logicModule_NotificationInd(uint8_t sender, OPERATION_HEADER_t* notification)
{
	
}

void logic_Handler(OPERATION_HEADER_t* operation_header)
{
	if(operation_header->opCode == LogicWrite)
	{
		LOGIC_WRITE_MESSAGE_t* msg = (LOGIC_WRITE_MESSAGE_t*)(operation_header + 1);
		if(!proccessDigitalPortAction(msg->address, false,  msg->value, msg->seconds, operation_header->sourceAddress))
		{
			//TODO:SEND ERROR MESSAGE (INVALID OPERATION)
		}
	}else if(operation_header->opCode == LogicSwitch)
	{
		LOGIC_SWITCH_MESSAGE_t* msg = (LOGIC_SWITCH_MESSAGE_t*)(operation_header + 1);
		if(!proccessDigitalPortAction(msg->address, false,  0xFF, msg->seconds, operation_header->sourceAddress))
		{
			//TODO:SEND ERROR MESSAGE (INVALID OPERATION)
		}
	}else if(operation_header->opCode == LogicRead)
	{
		LOGIC_READ_MESSAGE_t* msg = (LOGIC_READ_MESSAGE_t*)(operation_header + 1);
		if(!proccessDigitalPortAction(msg->address, true,  0, 0, operation_header->sourceAddress))
		{
			//TODO:SEND ERROR MESSAGE (INVALID OPERATION)
		}
	}else if(operation_header->opCode == LogicReadResponse)
	{
		//TODO: NOTIFICATION (check)
		//MODULES_Notify(LogicModule, operation_header);
	}
}

void logicRead_DataConf(OPERATION_DataConf_t *req)
{
	waitingForResponseConf = false;
}

uint8_t findLogicElem(uint16_t deviceAddress)
{
	for(uint8_t i = 0; i < num_of_logic_elems; i++)
	{
		if(logic_elems[i].config->deviceID == deviceAddress)
		return i;
	}
	
	return 0xFF;//Address not found
}

void sendDigitalResponse(uint16_t destinationAddress, uint16_t deviceAddress, uint8_t value)
{
	if(waitingForResponseConf)
	{
		//TODO:SEND ERROR MESSAGE (LOGIC RESPONSE OVERBOOKING)	
	}
	
	waitingForResponseConf = true;
	
	logicResponse.header.sourceAddress = runningConfiguration.topConfiguration.networkConfig.deviceAddress;
	logicResponse.header.destinationAddress = destinationAddress;
	logicResponse.response.address = deviceAddress;
	logicResponse.response.value = value;
	
	OM_ProccessResponseOperation(&logicResponse.header);
}

_Bool proccessDigitalPortAction(uint16_t deviceAddress, _Bool read, uint8_t value, uint8_t seconds, uint16_t sourceAddress)
{
	uint8_t configIndex = findLogicElem(deviceAddress);

	if(configIndex == 0xFF) //UNKNOWN DEVICE ADDRESS
	{
		sendDigitalResponse(sourceAddress, deviceAddress, 0xFF);
		return false;
	}
	
	LOGIC_ELEM_t* currentElem = &logic_elems[configIndex];
	
	if(read)
	{
		sendDigitalResponse(sourceAddress, deviceAddress, currentElem->lastValue);
	}
	else
	{
		if( currentElem->config->configBits.maskIO == 0 ) //If is Input -> INVALID OPERATION
		return false;
		
		if(value == 0xFF)//Switch action
		{
			HAL_GPIO_PORT_toggle(currentElem->portPtr, currentElem->mask);
			value = HAL_GPIO_PORT_read(currentElem->portPtr, currentElem->mask) == 0 ? 0 : 1;
		}else
		{
			if(value)
			{
				HAL_GPIO_PORT_set(currentElem->portPtr, currentElem->mask);
			}				
			else
			{
				HAL_GPIO_PORT_clr(currentElem->portPtr, currentElem->mask);
			}				
		}

		if(seconds > 0)
		{
			currentElem->timerCounter = (uint16_t)seconds*5; //Enable timer
		}			
		else
		{
			currentElem->timerCounter = 0; //Disable timer
		}			
			
		//Notify to the coordinator
		sendDigitalResponse(COORDINATOR_ADDRESS, deviceAddress, value);			
	}
	
	return true;
}

static void logicTimerHandler(SYS_Timer_t *timer)
{
	for(uint8_t i = 0; i < num_of_logic_elems; i++)
	{
		LOGIC_ELEM_t* currentElem = &logic_elems[i];
		CONFIG_MODULE_ELEM_HEADER_t* operationsInfoPtr = &currentElem->config->operationsInfo;
		LOGIC_BITS_CONFIG_t* configBitsPtr = &currentElem->config->configBits;

		//Check inputs
		if(configBitsPtr->maskIO == 0)
		{
			_Bool changeOcurred = 0;
			uint8_t val = HAL_GPIO_PORT_read(currentElem->portPtr, currentElem->mask) == 0 ? 0 : 1;
				
			//Debouncer
			if(val == currentElem->debouncerValue)//Valid value
			{
				//Check changes for inputs with changeType distinct of NO_EDGE
				if (configBitsPtr->changeType != NO_EDGE )
				{
					//Check for valid changes
					switch(configBitsPtr->changeType)
					{
						case FALLING_EDGE:	changeOcurred = (  currentElem->lastValue & ~val ); break;
						case RISIN_EDGE:	changeOcurred = ( ~currentElem->lastValue &  val ); break;
						case BOTH_EDGE:		changeOcurred = (  currentElem->lastValue != val ); break;
					}
					
					if(changeOcurred)
					{
						//TODO: USE OPERATION MANAGER!
						OPERATION_HEADER_t* operationPtr = &runningConfiguration.raw[operationsInfoPtr->pointerOperationList];
						for(int c = 0; c < operationsInfoPtr->numberOfOperations; c++)
						{
							OM_ProccessInternalOperation(operationPtr);
							operationPtr++;
						}
						
						//Notify to the coordinator
						sendDigitalResponse(COORDINATOR_ADDRESS, currentElem->config->deviceID, val);
					}
				}
					
				currentElem->lastValue = val;					
			}
				
			currentElem->debouncerValue = val;
		}
		else
		{			
			//If is OUTPUT Check TimerToggle
			if(timerDivider == 0)
			{
				// Timer check (Outputs)
				if(currentElem->timerCounter > 1)
				{
					currentElem->timerCounter--;
				}else if(currentElem->timerCounter == 1) //Time to process
				{
					//Disable timer
					currentElem->timerCounter = 0;
				
					//Invert current value
					HAL_GPIO_PORT_toggle(currentElem->portPtr, currentElem->mask);
				
					currentElem->lastValue = HAL_GPIO_PORT_read(currentElem->portPtr, currentElem->mask) == 0 ? 0 : 1;
				
					//Notify to the coordinator
					sendDigitalResponse(COORDINATOR_ADDRESS, currentElem->config->deviceID, currentElem->lastValue);
				}
			}
		}
	}
	
	if(timerDivider++ == 3)//4 * 50ms = 200 ms
		timerDivider = 0;
	
	(void)timer;
}