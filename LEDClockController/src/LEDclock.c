/*
===============================================================================
 Name        : LEDclock.c
 Author      : $(author)
 Version     :
 Copyright   : $(copyright)
 Description : main definition
===============================================================================
*/

#include <driver_config.h>
#include "target_config.h"
#include "system_LPC11xx.h"

#include <string.h>
#include <cr_section_macros.h>
#include <gpio.h>
#include <ssp.h>
#include <uart.h>

#include <stdio.h>
#include <stdlib.h>
#include "../../common/intpriority/i2c.h"

extern volatile uint32_t UARTCount;
extern volatile uint8_t UARTBuffer[BUFSIZE];

char command[10];
uint8_t com_length = 0;
char params[20];
uint8_t par_length = 0;
typedef enum
{
	WaitForStart,
	ParseCommand,
	ParseParams
} UARTReadState;
UARTReadState uart_state = WaitForStart;


extern volatile uint32_t I2CCount;
extern volatile uint8_t I2CMasterBuffer[BUFSIZE];
extern volatile uint8_t I2CSlaveBuffer[BUFSIZE];
extern volatile uint32_t I2CMasterState;
extern volatile uint32_t I2CReadLength, I2CWriteLength;

uint32_t counter = 0; // for 32kHz input from ds3231
uint32_t prev_counter = 0;

#define NUM_LEDS 30
//uint32_t leds[NUM_LEDS];
uint8_t leds_mask[NUM_LEDS];
uint32_t led_spi_buffer[NUM_LEDS*3*2]; // 2 words for every led colour

#define HOUR_LEFT		0
#define HOUR_RIGHT		7
#define DOT_BOTTOM		14
#define DOT_TOP			15
#define MINUTE_LEFT		16
#define MINUTE_RIGHT	23
uint32_t colour_on[NUM_LEDS]; // RGB array for all
uint32_t colour_off =	0x000000; // RGB
uint8_t brightness[NUM_LEDS]; // brightness per led

typedef enum
{
	ClockMode,
	InputMode,
	NormalColour,
	RainbowColour
} Mode;
Mode display_mode = ClockMode;
Mode colour_mode = NormalColour;

uint8_t numbers[] = {
    0b00111111, // 0
    0b00000110, // 1
    0b01011011, // 2
    0b01001111, // 3
    0b01100110, // 4
    0b01101101, // 5
    0b01111101, // 6
    0b00000111, // 7
    0b01111111, // 8
    0b01101111, // 9
    0b01100011, // º              10
    0b00111001, // C(elcius)      11
    0b01011100, // º lower        12
    0b00000000, // Empty          13
};

// interrupt handler for 32kHz input
void PIOINT1_IRQHandler(void)
{
	counter++;
	LPC_GPIO1->IC |= (0x1 << 5); //clear interrupt
}

// update buffer and send to LEDs
void update_leds(void)
{
	unsigned char cur_byte;
	unsigned char led = 0;
	for (int i=0; i<NUM_LEDS*3; i++)
	{
		// we need to write each LED's Green, then Red, then Blue colours in order
		switch (i%3)
		{
			case 0: // green
				cur_byte = leds_mask[led] ? (colour_on[led] >> 8) & 0xFF : 0x00;
				break;
			case 1: // red
				cur_byte = leds_mask[led] ? (colour_on[led] >> 16) & 0xFF : 0x00;
				break;
			case 2: // blue
				cur_byte = leds_mask[led] ? colour_on[led] & 0xFF : 0x00;
				break;
		}

//		printf("cb:%X, br:%X, *:%X, */:%X\n", cur_byte, brightness[led]+1, cur_byte*(brightness[led]+1), (int)((cur_byte*(brightness[led]+1)+128)/256));
		cur_byte = (cur_byte*(brightness[led]+1)+128) / 256;

		// emulate LED protocol using SPI
		// 110 represents a 1
		// 100 represents a 0
		led_spi_buffer[i*2+0] = 0b0100100100100;
		led_spi_buffer[i*2+1] = 0b0100100100100;

		if (cur_byte & 0x80)
			led_spi_buffer[i*2+0] |= (1<<10);
		if (cur_byte & 0x40)
			led_spi_buffer[i*2+0] |= (1<<7);
		if (cur_byte & 0x20)
			led_spi_buffer[i*2+0] |= (1<<4);
		if (cur_byte & 0x10)
			led_spi_buffer[i*2+0] |= (1<<1);

		if (cur_byte & 0x08)
			led_spi_buffer[i*2+1] |= (1<<10);
		if (cur_byte & 0x04)
			led_spi_buffer[i*2+1] |= (1<<7);
		if (cur_byte & 0x02)
			led_spi_buffer[i*2+1] |= (1<<4);
		if (cur_byte & 0x01)
			led_spi_buffer[i*2+1] |= (1<<1);
		if (i%3 == 2)
		{
			// move to next led if was blue (every third)
			led++;
		}
	}


    LPC_GPIO1->IE &= ~(0x1 << 5); // disable 32kHz interrupts

    SSP_Send(0, &led_spi_buffer[0], sizeof(led_spi_buffer)/sizeof(uint32_t));

    LPC_GPIO1->IE |= (0x1 << 5); // enable 32kHz interrupts
}

void write_number(uint8_t number, uint8_t start_pos)
{
	for (int i=0; i<7; i++)
	{
		leds_mask[i + start_pos] = (numbers[number] & (1 << i)) ? 1 : 0;
	}
}

void write_dots(uint8_t bottom, uint8_t top)
{
	leds_mask[DOT_BOTTOM] = bottom;
	leds_mask[DOT_TOP] = top;
}

uint32_t wheel(uint8_t pos)
{
	pos = 255 - pos;
	if (pos < 85)
	{
		return ((255 - pos*3) << 16) | (pos*3);
	}
	if (pos < 170)
	{
		pos -= 85;
		return ((pos*3) << 8) | (255 - pos*3);
	}
	pos -= 170;
	return ((pos*3) << 16) | ((255 - pos*3) << 8);
}

int main(void)
{
	printf("Start\n");

    for (int i=0; i<NUM_LEDS; i++)
    {
    	colour_on[i] = 0xFF0090;
    	brightness[i] = 255;

    	leds_mask[i] = 1;
    }


    // Set up SPI
    SSP_IOConfig(0);
    SSP_Init(0);

    leds_mask[DOT_BOTTOM] = 1;
    leds_mask[DOT_TOP] = 1;
    update_leds();

    UARTInit(UART_BAUD);

    if ( I2CInit( (uint32_t)I2CMASTER ) == 0 )	/* initialize I2c */
    {
    	while ( 1 );				/* Fatal error */
    }

    // RTC 32kHz output (pin 1 5)
    LPC_IOCON->PIO1_5 &= ~(0x3 << 3);
    LPC_IOCON->PIO1_5 |= (0x2 << 3); // enable pull-up resistor (required by ds3231)
    LPC_GPIO1->IS &= ~(0x1 << 5); // edge
    LPC_GPIO1->IBE &= ~(0x1 << 5); // single edge
    LPC_GPIO1->IEV &= ~(0x1 << 5); // falling edge
    LPC_GPIO1->IE |= (0x1 << 5); // enable interrupts
    NVIC_EnableIRQ(EINT1_IRQn); // register interrupt handler

    // Enable 32kHz output from RTC
    I2CWriteLength = 3;
    I2CReadLength = 0;
    I2CMasterBuffer[0] = DS3231_ADDR;
    I2CMasterBuffer[1] = 0x0F;		/* address */
    I2CMasterBuffer[2] = 0x08;		/* set bit 3 */
    I2CEngine();

    // Write time to RTC
    I2CWriteLength = 9;
    I2CReadLength = 0;
    I2CMasterBuffer[0] = DS3231_ADDR;
    I2CMasterBuffer[1] = 0x00;		/* address */
    I2CMasterBuffer[2] = 0x00;		/* Seconds */
    I2CMasterBuffer[3] = 0x10;		/* Minutes */
    I2CMasterBuffer[4] = 0x10;		/* Hours */
    I2CMasterBuffer[5] = 0x01;		/* Days */
    I2CMasterBuffer[6] = 0x01;		/* Date */
    I2CMasterBuffer[7] = 0x01;		/* Month */
    I2CMasterBuffer[8] = 0x20;		/* Year */
    I2CEngine();

    printf("Setup done\n");


    uint8_t angle = 0;
    uint32_t col;
    uint8_t num;
    uint8_t seconds, prev_minutes, minutes, hours;
    uint8_t ch;
    uint8_t command_received = 0;
    while(1)
    {
    	if ( UARTCount != 0 )
    	{
			LPC_UART->IER = IER_THRE | IER_RLS;			/* Disable RBR */
			for (int i=0; i<UARTCount; i++)
			{
				ch = UARTBuffer[i];
				switch (uart_state)
				{
					case WaitForStart:
						if (ch == '$')
						{
							uart_state = ParseCommand;
							com_length = 0;
							par_length = 0;
						}
						break;
					case ParseCommand:
						if (ch == ']')
						{
							uart_state = WaitForStart;
							command[com_length] = 0;
							command_received = 1;
							break;
						}
						if (ch == ',')
						{
							uart_state = ParseParams;
							command[com_length] = 0;
							break;
						}
						command[com_length++] = ch;
						break;
					case ParseParams:
						if (ch == ']')
						{
							uart_state = WaitForStart;
							params[par_length] = 0;
							command_received = 1;
							break;
						}
						params[par_length++] = ch;
						break;
					default:
						break;
				}
			}
			UARTCount = 0;
			LPC_UART->IER = IER_THRE | IER_RLS | IER_RBR;	/* Re-enable RBR */
    	}

    	if (command_received)
    	{
    		if (strcmp(command, "rgb") == 0)
    		{
    			colour_mode = NormalColour;
    			uint8_t start = 0;
    			uint8_t end = NUM_LEDS;
    			if (strncmp(params, "all", 3) == 0)
    			{
    				start = 0;
    				end = NUM_LEDS;
    			}
    			else if (strncmp(params, "hrs", 3) == 0)
    			{
    				start = 0;
    				end = DOT_BOTTOM;
    			}
    			else if (strncmp(params, "mns", 3) == 0)
    			{
    				start = MINUTE_LEFT;
    				end = NUM_LEDS;
    			}
    			else if (strncmp(params, "dts", 3) == 0)
    			{
    				start = DOT_BOTTOM;
    				end = MINUTE_LEFT;
    			}
//    			sscanf(&params[4], "%X", &col);
    			col = strtol(&params[4], NULL, 16);
    			for (int i=start; i<end; i++)
    			{
    				colour_on[i] = col;
    			}
    			update_leds();
    		}
    		else if (strcmp(command, "brightness") == 0)
    		{
    			uint8_t start = 0;
    			uint8_t end = NUM_LEDS;
    			if (strncmp(params, "all", 3) == 0)
    			{
    				start = 0;
    				end = NUM_LEDS;
    			}
    			else if (strncmp(params, "hrs", 3) == 0)
    			{
    				start = 0;
    				end = DOT_BOTTOM;
    			}
    			else if (strncmp(params, "mns", 3) == 0)
    			{
    				start = MINUTE_LEFT;
    				end = NUM_LEDS;
    			}
    			else if (strncmp(params, "dts", 3) == 0)
    			{
    				start = DOT_BOTTOM;
    				end = MINUTE_LEFT;
    			}
    			num = strtol(&params[4], NULL, 10);
    			for (int i=start; i<end; i++)
    			{
    				brightness[i] = num;
    			}
    			update_leds();
    		}
    		else if (strcmp(command, "toggle") == 0)
    		{
    			display_mode = InputMode;
    			if (params[0] == 'c')
    			{
    				for (int i=0; i<NUM_LEDS; i++)
    				{
    					leds_mask[i] = 0;
    				}
    			}
    			else if (params[0] == 'i')
    			{
    				for (int i=0; i<NUM_LEDS; i++)
    				{
    					leds_mask[i] ^= 1;
    				}
    			}
    			else
    			{
    				num = atoi(params);
					leds_mask[num] ^= 1;
    			}
				update_leds();
    		}
    		else if (strcmp(command, "time") == 0)
    		{
    			uint32_t now = 0;
    			uint32_t date = 0;
    			// cant read entire datetime in one go
    			// copy first 6 (yyMMdd) to temp string, read that
    			// then read last 6 (HHmmss)
    			char temp[7];
    			strncpy(temp, params, 6);
    			temp[6] = 0;
    			sscanf(temp, "%LX", &date);
    			sscanf(&params[6], "%LX", &now);
    			// Write time to RTC
    		    I2CWriteLength = 9;
    		    I2CReadLength = 0;
    		    I2CMasterBuffer[0] = DS3231_ADDR;
    		    I2CMasterBuffer[1] = 0x00;					/* address */
    		    I2CMasterBuffer[2] = (now >> 0 & 0xFF);		/* Seconds */
    		    I2CMasterBuffer[3] = (now >> 8 & 0xFF);		/* Minutes */
    		    I2CMasterBuffer[4] = (now >> 16 & 0xFF);		/* Hours */
    		    I2CMasterBuffer[5] = 0x01;					/* Days */
    		    I2CMasterBuffer[6] = (date >> 0 & 0xFF);	/* Date */
    		    I2CMasterBuffer[7] = (date >> 8 & 0xFF);	/* Month */
    		    I2CMasterBuffer[8] = (date >> 16 & 0xFF);	/* Year */
    		    I2CEngine();
//    		    printf("%X, %X, %X\n", (now>>16&0xFF), (now>>8&0xFF), (now>>0&0xFF));
    		}
    		else if (strcmp(command, "showclock") == 0)
    		{
    			display_mode = ClockMode;
				write_number((hours >> 4) & 0xF, HOUR_LEFT);
				write_number((hours >> 0) & 0xF, HOUR_RIGHT);
				write_number((minutes >> 4) & 0xF, MINUTE_LEFT);
				write_number((minutes >> 0) & 0xF, MINUTE_RIGHT);
				write_dots(1, 1);
				update_leds();
    		}
    		else if (strcmp(command, "rainbow") == 0)
    		{
    			colour_mode = RainbowColour;
    		}

    		command_received = 0;
    	}

    	if (colour_mode == RainbowColour)
    	{
			// every 0.2 seconds
			if (((counter - prev_counter) > (32768 * 2)) || (counter < prev_counter))
			{
				prev_counter = counter;

				angle++;
				col = wheel(angle);
				for (int i = 0; i < NUM_LEDS; i++)
				{
					colour_on[i] = col;
				}
				update_leds();
			}
    	}

    	if (display_mode == ClockMode)
    	{
			// every 10 seconds
			if (counter > (32768 * 10))
			{
				counter = 0;

				// Read seconds minutes and hours from rtc.
				I2CWriteLength = 2;
				I2CReadLength = 3;
				I2CMasterBuffer[0] = DS3231_ADDR;
				I2CMasterBuffer[1] = 0x00;		/* address */
				I2CMasterBuffer[2] = DS3231_ADDR | RD_BIT;
				I2CEngine();

				seconds = I2CSlaveBuffer[0];
				minutes = I2CSlaveBuffer[1];
				hours = I2CSlaveBuffer[2];

				if (minutes != prev_minutes)
				{
					prev_minutes = minutes;

					write_number((hours >> 4) & 0xF, HOUR_LEFT);
					write_number((hours >> 0) & 0xF, HOUR_RIGHT);
					write_number((minutes >> 4) & 0xF, MINUTE_LEFT);
					write_number((minutes >> 0) & 0xF, MINUTE_RIGHT);
					write_dots(1, 1);
					update_leds();
				}
			}
    	}
    }
    return 0 ;
}
