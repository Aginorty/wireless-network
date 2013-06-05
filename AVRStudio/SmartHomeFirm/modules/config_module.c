/*
 * config_module.c
 *
 * Created: 31/05/2013 19:41:10
 *  Author: Victor
 */ 
#include "modulesManager.h"
#include "globals.h"
#include <util/crc16.h>

enum
{
	OK = 0,
	ERROR_FRAGMENT_TOTAL_NOT_EXPECTED,
	ERROR_FRAGMENT_ORDER,
	ERROR_WAITING_FIRST_FRAGMENT,
	ERROR_CONFIG_SIZE_TOO_BIG,
	ERROR_CONFIG_INVALID_CHECKSUM,
	ERROR_CONFIG_SIZE_NOT_EXPECTED,
	ERROR_BUSY_RECEIVING_STATE
}RESPONSE_ERROR_CODES;

struct
{
	OPERATION_HEADER_t header;
	CONFIG_WRITE_RESPONSE_HEADER_MESSAGE_t response;
}configWriteResponse;

struct
{
	OPERATION_HEADER_t header;
	CONFIG_CHECKSUM_RESPONSE_MESSAGE_t response;
}checksumResponse;

_Bool receivingState;
uint8_t currentRecvFragment;
uint8_t totalRecvExpected;
uint16_t currentRecvIndex;

_Bool sendingState;
uint8_t currentSendFragment;
uint8_t totalSendExpected;
uint16_t currentSendIndex;

RUNNING_CONFIGURATION_t configBuffer;

_Bool validateReceivedConfig(void);

void configModule_Init(void)
{
	configWriteResponse.header.opCode = ConfigWriteResponse;
	checksumResponse.header.opCode = ConfigChecksumResponse;
	
	receivingState = false;
	currentRecvIndex = 0;
}

void configModule_NotificationInd(uint8_t sender, OPERATION_HEADER_t* notification)
{
	
}

void configWrite_Handler(OPERATION_HEADER_t* operation_header)
{
	if(operation_header->opCode == ConfigWrite)
	{
		_Bool acceptFragment = false;
		CONFIG_WRITE_HEADER_MESSAGE_t* msg = (CONFIG_WRITE_HEADER_MESSAGE_t*)(operation_header + 1);
		
		configWriteResponse.header.destinationAddress = operation_header->sourceAddress;
		configWriteResponse.header.sourceAddress = runningConfiguration.topConfiguration.networkConfig.deviceAddress;
		configWriteResponse.response.fragment = msg->fragment;
		configWriteResponse.response.fragmentTotal = msg->fragmentTotal;
		
		if(receivingState)
		{
			if(msg->fragmentTotal != totalRecvExpected)
			{
				receivingState = false;
				configWriteResponse.response.code = ERROR_FRAGMENT_TOTAL_NOT_EXPECTED;
			}else if(msg->fragment == (currentRecvFragment+1))
			{
				currentRecvFragment++;
				acceptFragment= true;
			}else
			{
				receivingState = false;
				configWriteResponse.response.code = ERROR_FRAGMENT_ORDER;
			}
		}else if(msg->fragment == 0) //FirstFrame
		{
			currentRecvFragment = 0;
			receivingState = true;
			totalRecvExpected = msg->fragmentTotal;
			acceptFragment = true;
			configWriteResponse.response.code = OK;
		}else
		{
			receivingState = false;
			configWriteResponse.response.code = ERROR_WAITING_FIRST_FRAGMENT;
		}
		
		if(acceptFragment)
		{
			if((msg->length + currentRecvIndex) >= EEPROM_SIZE)
			{
				configWriteResponse.response.code = ERROR_CONFIG_SIZE_TOO_BIG;
			}else
			{	
				memcpy((uint8_t*)&configBuffer.raw[currentRecvIndex], (uint8_t*)(msg + 1), sizeof(uint8_t) * msg->length);
				currentRecvIndex += (uint16_t)msg->length;
			
				if(currentRecvFragment == msg->fragmentTotal)//ALL RECEIVED
				{
					if(validateReceivedConfig())
					{
						EEPROM_Write_Block(configBuffer.raw, 0x00, configBuffer.topConfiguration.deviceInfo.length);
						softReset();
					}else
					{
						configWriteResponse.response.code = ERROR_CONFIG_INVALID_CHECKSUM;
					}
					receivingState = false;
					totalRecvExpected = 0;
				}
			}			
		}
		
		OM_ProccessResponseOperation(&configWriteResponse.header);
	}
}


void configRead_Handler(OPERATION_HEADER_t* operation_header)
{
	if(operation_header->opCode == ConfigRead)
	{
		if(sendingState)
		{
			//TODO: SEND OR LOG ERROR (BUSY SENDING STATE)
		}else
		{
		
		}
	}else if(operation_header->opCode == ConfigReadResponse)
	{
		//TODO: NOTIFICATION
	}		
}

void configChecksum_Handler(OPERATION_HEADER_t* operation_header)
{
	if(operation_header->opCode == ConfigChecksum)
	{
		checksumResponse.header.destinationAddress = operation_header->sourceAddress;
		checksumResponse.header.sourceAddress = runningConfiguration.topConfiguration.networkConfig.deviceAddress;
		checksumResponse.response.checksum = runningConfiguration.topConfiguration.deviceInfo.checkSum;
		
		OM_ProccessResponseOperation(&checksumResponse.header);
	}else if(operation_header->opCode == ConfigChecksumResponse)
	{
		//TODO: NOTIFICATION
	}		
}

_Bool validateReceivedConfig()
{
	uint16_t eeprom_size = configBuffer.topConfiguration.deviceInfo.length;
	uint16_t eeprom_crc = configBuffer.topConfiguration.deviceInfo.checkSum;
	
	if(eeprom_size != 0xFFFF && eeprom_size != 0x00 && eeprom_size == currentRecvIndex)
	{
		configBuffer.topConfiguration.deviceInfo.checkSum = 0;
		
		uint16_t acc = 0;
		for(int i = 0; i < eeprom_size; i++)
		acc = _crc16_update(acc, configBuffer.raw[i]);
		
		configBuffer.topConfiguration.deviceInfo.checkSum = eeprom_crc;
		
		return eeprom_crc == acc;
	}else
	{
		//TODO: SEND OR LOG ERROR (ERROR CONFIG SIZE NOT EXPECTED)
	}
	
	return false;
}