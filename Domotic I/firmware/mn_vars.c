//-----------------------------------------------------------------------------
// mn_vars.c
//-----------------------------------------------------------------------------
// Copyright 2006 Silicon Laboratories, Inc.
//
// Description:
// 	This file contains variables used by the TCP/IP Stack.
//
// Generated by TCP/IP Library Builder Version 3.23.
//

#include "mn_defs.h"
#include "mn_userconst.h"

//-----------------------------------------------------------------------------
// IP Address Setup
//-----------------------------------------------------------------------------
//
//   Change the ip_src_addr below to select the IP address of
//   your target. If the IP address of the destination is known
//   replace ip_dest_addr with that address. If you are dialing
//   into an ISP, the source and destination addresses initially
//   specified do not matter as they will be negotiated.
//

byte ip_dest_addr[IP_ADDR_LEN] = IP_DEST_ADDR;
byte ip_src_addr[IP_ADDR_LEN]  = IP_SRC_ADDR;

//-----------------------------------------------------------------------------
// MAC Address Setup
//-----------------------------------------------------------------------------
//
//   If using the CP2200, mn_ether_init will overwrite this value with the
//   MAC address stored in the Flash of the CP2200.
//
//   If using another Ethernet controller with EEPROM you may need to write
//   a routine to take the value of the hw_addr in EEPROM and put it into
//   the array below on startup. Otherwise replace eth_src_hw_addr
//   below with the proper ethernet hardware address.
//

byte eth_src_hw_addr[ETH_ADDR_LEN] = ETH_SRC_HW_ADDR;

//   ARP is used, so the array below is used as a temporary holder for
//   the destination hardware address. It does not have to be changed.
byte eth_dest_hw_addr[ETH_ADDR_LEN] = ETH_DEST_HW_ADDR;

//   If a gateway is being used set the gateway IP address and
//   subnet mask below.
//
//   If a gateway is not being used:
//      set the gateway IP address to { 255,255,255,255 }
//      set the subnet mask to        { 255,255,255,  0 }
//
byte gateway_ip_addr[IP_ADDR_LEN] = GATEWAY_IP_ADDR;
byte subnet_mask[IP_ADDR_LEN]     = SUBNET_MASK_ADDR;

//-----------------------------------------------------------------------------
// Stack Variable Declarations
//-----------------------------------------------------------------------------
//
// These arrays are used by the TCP/IP stack. They cannot be removed or edited.
//
SOCKET_INFO_T sock_info[num_sockets];
byte null_addr[IP_ADDR_LEN];

byte recv_buff[recv_buff_size];

#if (!c8051f340_usb_fifo)
byte send_buff[xmit_buff_size];
#else
byte send_buff[1] _at_ 0x400;
#endif

byte ping_reply_buff[ping_buff_size + 9];

VF_PTR http_vf_ptrs[num_sockets];
TEMP_SSI_S temp_ssi_info[num_sockets];
byte HTTPBuffer[http_buffer_len];
byte URIBuffer[uri_buffer_len];
byte * const URIBuffer_max = &URIBuffer[uri_buffer_len-1];
byte BODYBuffer[body_buffer_len];

VF vf_dir[num_vf_pages];
PAGE_SEND_T page_send_info[num_sockets];
POST_FUNCS pf[num_post_funcs];

ARP_INFO_T arp_info[arp_cache_size];

//-----------------------------------------------------------------------------
// FLASH Constants
//-----------------------------------------------------------------------------
//
// These constants define the behavior of the stack. They should not be changed
// or edited here.
//
// To change the value of a FLASH constant, modify its corresponding
// preprocessor-defined constant in mn_userconst.h.
//

//--------------------------
// Device
//--------------------------
byte     code  DEVICE_ID = device_id;
byte     code  C8051F340_USB_FIFO = c8051f340_usb_fifo;

//--------------------------
// Sockets
//--------------------------
byte     code	NUM_SOCKETS = num_sockets;
word16	code	XMIT_BUFF_SIZE = xmit_buff_size;
word16	code	RECV_BUFF_SIZE = recv_buff_size;
word16	code	SOCKET_WAIT_TICKS = socket_wait_ticks;

//--------------------------
// Ethernet
//--------------------------
word16	code	ETHER_WAIT_TICKS = ether_wait_ticks;

//--------------------------
// ARP
//--------------------------
word16   code  ARP_KEEP_TICKS = arp_keep_ticks;
byte     code  ARP_RESEND_TRYS = arp_resend_trys;
word16   code  ARP_WAIT_TICKS = arp_wait_ticks;
byte     code  ARP_CACHE_SIZE = arp_cache_size;
byte     code  ARP_AUTO_UPDATE = arp_auto_update;

//--------------------------
// HTTP
//--------------------------
unsigned char  code BODY_BUFFER_LEN = body_buffer_len;
word16   code  HTTP_BUFFER_LEN = http_buffer_len;

//--------------------------
// TFTP
//--------------------------
int   code  TFTP_RESEND_TRYS = tftp_resend_trys;

//--------------------------
// IP
//--------------------------
byte  code  TIME_TO_LIVE = ip_time_to_live;
byte  code  MULTICAST_TTL = multicast_ttl;

//--------------------------
// PING
//--------------------------
word16   code  PING_BUFF_SIZE = ping_buff_size;

//--------------------------
// TCP
//--------------------------
word16   code  TCP_WINDOW = tcp_window;
word16   code  TCP_RESEND_TICKS = tcp_resend_ticks;
byte     code  TCP_RESEND_TRYS = tcp_resend_trys;

//--------------------------
// Virtual File
//--------------------------
byte  code  NUM_VF_PAGES = num_vf_pages;
byte  code  NUM_POST_FUNCS = num_post_funcs;

//--------------------------
// Timer
//--------------------------
byte	code	TL0_FLASH = tl0_flash;
byte	code	TH0_FLASH = th0_flash;
